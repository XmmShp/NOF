using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Infrastructure.Memory;
using Xunit;

namespace NOF.Infrastructure.Tests.Persistence;

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
        Assert.Null(
        tenant);
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
        Assert.NotNull(

        (await repository.FindAsync("outer")));
        Assert.Null(
        (await repository.FindAsync("inner")));
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
        Assert.Null(

        (await repository.FindAsync("tenant-dispose")));
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

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("LIFO order", ex.Message);

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
        await ((IRepository<TestOrder, long>)repository).FindAsync(1L);

        var changeCount = await unitOfWork.SaveChangesAsync();
        Assert.Equal(1,

        changeCount);
        Assert.IsType<TestEvent>(Assert.Single(publisher.Events));
        Assert.Empty(
        order.Events);
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
        Assert.Equal(0,

        second);
    }

    [Fact]
    public async Task InboxRepository_ShouldAddFindAndRemoveMessages()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>();
        var message = new NOFInboxMessage(Guid.NewGuid());

        repository.Add(message);
        Assert.True(

        (await repository.ExistsAsync(message.Id)));
        Assert.NotNull(
        (await repository.FindAsync(message.Id)));

        repository.Remove(message);
        Assert.False(

        (await repository.ExistsAsync(message.Id)));
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
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        });

        var claimed = await repository.AtomicClaimPendingMessagesAsync().ToListAsync();

        Assert.Single(claimed);
        Assert.Equal(1,
        claimed[0].RetryCount);
        Assert.False(string.IsNullOrWhiteSpace(claimed[0].ClaimedBy));

        await repository.AtomicMarkAsSentAsync([claimed[0].Id]);

        var stored = await repository.FindAsync(claimed[0].Id);
        Assert.NotNull(
        stored);
        Assert.Equal(OutboxMessageStatus.Sent,
        stored.Status);
        Assert.NotNull(
        stored.SentAt);
    }

    [Fact]
    public async Task OutboxRepository_ShouldNotClaimExpiredOrExhaustedMessagesBeyondRules()
    {
        using var services = CreateServiceProvider(new OutboxOptions { MaxRetryCount = 2, ClaimTimeout = TimeSpan.FromMinutes(1) });
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();

        var id1 = Guid.NewGuid();
        repository.Add(new NOFOutboxMessage
        {
            Id = id1,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            Payload = "a",
            Headers = "{}",
            MessageType = OutboxMessageType.Command
        });

        var id2 = Guid.NewGuid();
        repository.Add(new NOFOutboxMessage
        {
            Id = id2,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            Payload = "b",
            Headers = "{}",
            MessageType = OutboxMessageType.Command
        });

        var exhausted = await repository.FindAsync(id1);
        Assert.NotNull(
        exhausted);
        exhausted.RetryCount = 2;

        var claimedUntilFuture = await repository.FindAsync(id2);
        Assert.NotNull(
        claimedUntilFuture);
        claimedUntilFuture.ClaimExpiresAt = DateTime.UtcNow.AddMinutes(5);

        var claimed = await repository.AtomicClaimPendingMessagesAsync().ToListAsync();
        Assert.Empty(

        claimed);
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
        Assert.NotNull(
        stored);
        Assert.Equal(OutboxMessageStatus.Failed,
        stored.Status);
        Assert.Equal("boom",
        stored.ErrorMessage);
    }

    [Fact]
    public async Task StateMachineRepository_ShouldIsolateDataByTenant()
    {
        using var services = CreateServiceProvider();
        using (var hostScope = services.CreateScope())
        {
            var hostExecutionContext = hostScope.ServiceProvider.GetRequiredService<IExecutionContext>();
            hostExecutionContext.SetTenantId(NOFContractConstants.Tenant.HostId);
            var hostRepository = hostScope.ServiceProvider.GetRequiredService<IStateMachineContextRepository>();
            hostRepository.Add(new NOFStateMachineContext { CorrelationId = "corr", DefinitionTypeName = "def", State = 1 });
        }

        using (var tenantScope = services.CreateScope())
        {
            var tenantExecutionContext = tenantScope.ServiceProvider.GetRequiredService<IExecutionContext>();
            tenantExecutionContext.SetTenantId("tenant-a");
            var tenantRepository = tenantScope.ServiceProvider.GetRequiredService<IStateMachineContextRepository>();
            tenantRepository.Add(new NOFStateMachineContext { CorrelationId = "corr", DefinitionTypeName = "def", State = 2 });
            Assert.Equal(2,
            (await tenantRepository.FindAsync("corr", "def"))!.State);
        }

        using (var verifyHostScope = services.CreateScope())
        {
            var verifyHostExecutionContext = verifyHostScope.ServiceProvider.GetRequiredService<IExecutionContext>();
            verifyHostExecutionContext.SetTenantId(NOFContractConstants.Tenant.HostId);
            var verifyHostRepository = verifyHostScope.ServiceProvider.GetRequiredService<IStateMachineContextRepository>();
            Assert.Equal(1,
            (await verifyHostRepository.FindAsync("corr", "def"))!.State);
        }
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
        Assert.NotNull(
        order);
        Assert.Equal("custom",
        order.Number);
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
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("NOF is using NOF.Infrastructure.Memory for persistence fallback")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static ServiceProvider CreateServiceProvider(OutboxOptions? outboxOptions = null, ILogger<MemoryPersistenceWarningHostedService>? warningLogger = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<MemoryPersistenceStore>();
        services.AddScoped<IExecutionContext, NOF.Contract.ExecutionContext>();
        services.AddScoped<IUserContext, NOF.Infrastructure.UserContext>();
        services.AddScoped(sp => sp.GetRequiredService<MemoryPersistenceStore>().CreateContext(sp.GetRequiredService<IExecutionContext>().TenantId));
        services.AddScoped<IUnitOfWork, MemoryUnitOfWork>();
        services.AddScoped<ITransactionManager, MemoryTransactionManager>();
        services.AddScoped<IInboxMessageRepository, MemoryInboxMessageRepository>();
        services.AddScoped<IOutboxMessageRepository, MemoryOutboxMessageRepository>();
        services.AddScoped<ITenantRepository, MemoryTenantRepository>();
        services.AddScoped<IStateMachineContextRepository, MemoryStateMachineContextRepository>();
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

    private sealed class TestOrder : AggregateRoot, ICloneable
    {
        public long Id { get; init; }

        public string Number { get; init; } = string.Empty;

        public static TestOrder Create(long id, string number)
            => new() { Id = id, Number = number };

        public void Raise(IEvent @event)
            => AddEvent(@event);

        public object Clone()
            => Create(Id, Number);
    }

    private sealed class TestOrderRepository : MemoryRepository<TestOrder, long>
    {
        public TestOrderRepository(MemoryPersistenceContext context)
            : base(context, static order => order.Id)
        {
        }
    }
}


