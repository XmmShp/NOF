using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NOF.Application;
using NOF.Domain;
using NOF.Infrastructure.Abstraction;
using NOF.Infrastructure.Core;
using Xunit;

namespace NOF.Infrastructure.Core.Tests.Persistence;

public class InMemoryPersistenceTests
{
    [Fact]
    public async Task Transaction_Rollback_ShouldRestorePreviousState()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();

        await using var transaction = await transactionManager.BeginTransactionAsync();
        repository.Add(new NOFTenant { Id = "tenant-1", Name = "Tenant 1" });
        await transaction.RollbackAsync();

        var tenant = await repository.FindAsync("tenant-1");
        tenant.Should().BeNull();
    }

    [Fact]
    public async Task Transaction_NestedRollback_ShouldRestoreInnerChangesOnly()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();

        await using var outer = await transactionManager.BeginTransactionAsync();
        repository.Add(new NOFTenant { Id = "outer", Name = "Outer" });

        await using var inner = await transactionManager.BeginTransactionAsync();
        repository.Add(new NOFTenant { Id = "inner", Name = "Inner" });
        await inner.RollbackAsync();
        await outer.CommitAsync();

        (await repository.FindAsync("outer")).Should().NotBeNull();
        (await repository.FindAsync("inner")).Should().BeNull();
    }

    [Fact]
    public async Task Transaction_DisposeWithoutCompletion_ShouldRollbackChanges()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();

        await using (await transactionManager.BeginTransactionAsync())
        {
            repository.Add(new NOFTenant { Id = "tenant-dispose", Name = "Dispose" });
        }

        (await repository.FindAsync("tenant-dispose")).Should().BeNull();
    }

    [Fact]
    public async Task Transaction_CompleteOutOfOrder_ShouldThrow()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();

        await using var outer = await transactionManager.BeginTransactionAsync();
        await using var inner = await transactionManager.BeginTransactionAsync();

        Func<Task> act = () => outer.CommitAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*LIFO order*");

        await inner.RollbackAsync();
        await outer.RollbackAsync();
    }

    [Fact]
    public async Task UnitOfWork_SaveChanges_ShouldPublishEvents_ClearEvents_AndReturnChangeCount()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<TestOrderRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var publisher = scope.ServiceProvider.GetRequiredService<TestEventPublisher>();

        var order = TestOrder.Create(1, "order-1");
        order.Raise(new TestEvent("created"));
        repository.Add(order);

        var changeCount = await unitOfWork.SaveChangesAsync();

        changeCount.Should().Be(1);
        publisher.Events.Should().ContainSingle().Which.Should().BeOfType<TestEvent>();
        order.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task UnitOfWork_SaveChanges_TwiceWithoutNewChanges_ShouldReturnZero()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<TestOrderRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        repository.Add(TestOrder.Create(1, "order-1"));

        await unitOfWork.SaveChangesAsync();
        var second = await unitOfWork.SaveChangesAsync();

        second.Should().Be(0);
    }

    [Fact]
    public async Task InboxRepository_ShouldAddFindAndRemoveMessages()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>();
        var message = new NOFInboxMessage(Guid.NewGuid());

        repository.Add(message);

        (await repository.ExistsAsync(message.Id)).Should().BeTrue();
        (await repository.FindAsync(message.Id)).Should().NotBeNull();

        repository.Remove(message);

        (await repository.ExistsAsync(message.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task OutboxRepository_ShouldClaimAndMarkMessagesAsSent()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();

        repository.Add(new NOFOutboxMessage
        {
            PayloadType = typeof(string).AssemblyQualifiedName!,
            Payload = "payload",
            Headers = "{}",
            MessageType = OutboxMessageType.Command,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        var claimed = await repository.AtomicClaimPendingMessagesAsync().ToListAsync();

        claimed.Should().ContainSingle();
        claimed[0].RetryCount.Should().Be(1);
        claimed[0].ClaimedBy.Should().NotBeNullOrWhiteSpace();

        await repository.AtomicMarkAsSentAsync([claimed[0].Id]);

        var stored = await repository.FindAsync(claimed[0].Id);
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(OutboxMessageStatus.Sent);
        stored.SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task OutboxRepository_ShouldNotClaimExpiredOrExhaustedMessagesBeyondRules()
    {
        using var services = CreateServiceProvider(new OutboxOptions { MaxRetryCount = 2, ClaimTimeout = TimeSpan.FromMinutes(1) });
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();

        repository.Add(new NOFOutboxMessage
        {
            Id = 1,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            Payload = "a",
            Headers = "{}",
            MessageType = OutboxMessageType.Command
        });

        repository.Add(new NOFOutboxMessage
        {
            Id = 2,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            Payload = "b",
            Headers = "{}",
            MessageType = OutboxMessageType.Command
        });

        var exhausted = await repository.FindAsync(1L);
        exhausted.Should().NotBeNull();
        exhausted!.RetryCount = 2;

        var claimedUntilFuture = await repository.FindAsync(2L);
        claimedUntilFuture.Should().NotBeNull();
        claimedUntilFuture!.ClaimExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

        var claimed = await repository.AtomicClaimPendingMessagesAsync().ToListAsync();

        claimed.Should().BeEmpty();
    }

    [Fact]
    public async Task OutboxRepository_ShouldMarkMessageAsFailed_WhenRetryCountReachesLimit()
    {
        using var services = CreateServiceProvider(new OutboxOptions { MaxRetryCount = 1, ClaimTimeout = TimeSpan.FromMinutes(1) });
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();

        repository.Add(new NOFOutboxMessage
        {
            PayloadType = typeof(string).AssemblyQualifiedName!,
            Payload = "payload",
            Headers = "{}",
            MessageType = OutboxMessageType.Notification
        });

        var claimed = await repository.AtomicClaimPendingMessagesAsync().ToListAsync();
        await repository.AtomicRecordDeliveryFailureAsync(claimed[0].Id, "boom");

        var stored = await repository.FindAsync(claimed[0].Id);
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(OutboxMessageStatus.Failed);
        stored.ErrorMessage.Should().Be("boom");
    }

    [Fact]
    public async Task StateMachineRepository_ShouldIsolateDataByTenant()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        var invocationContext = scope.ServiceProvider.GetRequiredService<IMutableInvocationContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IStateMachineContextRepository>();

        invocationContext.SetTenantId(null);
        repository.Add(new NOFStateMachineContext { CorrelationId = "corr", DefinitionTypeName = "def", State = 1 });

        invocationContext.SetTenantId("tenant-a");
        repository.Add(new NOFStateMachineContext { CorrelationId = "corr", DefinitionTypeName = "def", State = 2 });

        (await repository.FindAsync("corr", "def"))!.State.Should().Be(2);

        invocationContext.SetTenantId(null);
        (await repository.FindAsync("corr", "def"))!.State.Should().Be(1);
    }

    [Fact]
    public void Store_GetPartition_WithSameNameButDifferentTypes_ShouldThrow()
    {
        var store = new InMemoryPersistenceStore();

        store.GetPartition<TestOrder, long>("orders", static order => order.Id, static order => TestOrder.Create(order.Id, order.Number));

        var act = () => store.GetPartition<TestTenantProjection, string>("orders", static projection => projection.Id, static projection => new TestTenantProjection { Id = projection.Id });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered with a different entity or key type*");
    }

    [Fact]
    public async Task BusinessRepositoryBase_ShouldSupportCustomRepositoryReuse()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<TestOrderRepository>();
        var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();

        await using var transaction = await transactionManager.BeginTransactionAsync();
        repository.Add(TestOrder.Create(42, "custom"));
        await transaction.CommitAsync();

        var order = await ((IRepository<TestOrder, long>)repository).FindAsync(42L);
        order.Should().NotBeNull();
        order!.Number.Should().Be("custom");
    }

    [Fact]
    public async Task WarningHostedService_ShouldLogWarning_WhenUsingBuiltInPersistence()
    {
        var logger = new Mock<ILogger<MemoryPersistenceWarningHostedService>>();
        using var services = CreateServiceProvider(warningLogger: logger.Object);
        var hostedService = new MemoryPersistenceWarningHostedService(services, logger.Object);

        await hostedService.StartAsync(CancellationToken.None);

        logger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("built-in in-memory persistence implementation")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static ServiceProvider CreateServiceProvider(OutboxOptions? outboxOptions = null, ILogger<MemoryPersistenceWarningHostedService>? warningLogger = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<InMemoryPersistenceStore>();
        services.AddScoped<InMemoryPersistenceSession>();
        services.AddScoped<IMutableUserContext, UserContext>();
        services.AddScoped<IUserContext>(sp => sp.GetRequiredService<IMutableUserContext>());
        services.AddScoped<IMutableInvocationContext, InvocationContext>();
        services.AddScoped<IInvocationContext>(sp => sp.GetRequiredService<IMutableInvocationContext>());
        services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();
        services.AddScoped<ITransactionManager, InMemoryTransactionManager>();
        services.AddScoped<IInboxMessageRepository, InMemoryInboxMessageRepository>();
        services.AddScoped<IOutboxMessageRepository, InMemoryOutboxMessageRepository>();
        services.AddScoped<ITenantRepository, InMemoryTenantRepository>();
        services.AddScoped<IStateMachineContextRepository, InMemoryStateMachineContextRepository>();
        services.AddScoped<TestOrderRepository>();
        services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        services.AddSingleton<TestEventPublisher>();
        services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<TestEventPublisher>());
        services.AddSingleton(Options.Create(outboxOptions ?? new OutboxOptions()));
        services.AddLogging();

        if (warningLogger is not null)
        {
            services.AddSingleton(warningLogger);
        }

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        private long _current = 1000;

        public long NextId() => Interlocked.Increment(ref _current);
    }

    private sealed class TestEventPublisher : IEventPublisher
    {
        public List<IEvent> Events { get; } = [];

        public Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed record TestEvent(string Name) : IEvent;

    private sealed class TestOrder : AggregateRoot
    {
        public long Id { get; init; }

        public string Number { get; init; } = string.Empty;

        public static TestOrder Create(long id, string number)
            => new() { Id = id, Number = number };

        public void Raise(IEvent @event)
            => AddEvent(@event);
    }

    private sealed class TestOrderRepository : InMemoryRepository<TestOrder, long>
    {
        public TestOrderRepository(InMemoryPersistenceStore store, InMemoryPersistenceSession session)
            : base(store, session, "test:orders", static order => order.Id, static order => TestOrder.Create(order.Id, order.Number))
        {
        }
    }

    private sealed class TestTenantProjection
    {
        public string Id { get; init; } = string.Empty;
    }
}
