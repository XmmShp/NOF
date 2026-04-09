using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Hosting;
using NOF.Infrastructure.EntityFrameworkCore;
using NOF.Infrastructure.EntityFrameworkCore.SQLite;
using NOF.Infrastructure.Memory;
using Xunit;

namespace NOF.Infrastructure.Tests.Persistence;

public class SqliteInMemoryPersistenceTests
{
    [Fact]
    public async Task Transaction_Rollback_ShouldRestorePreviousState()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);

        var repository = scope.ServiceProvider.GetRequiredService<IRepository<NOFTenant, string>>();
        var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await using var transaction = await transactionManager.BeginTransactionAsync();
        repository.Add(new NOFTenant { Id = "tenant-1", Name = "Tenant 1" });
        await unitOfWork.SaveChangesAsync();
        await transaction.RollbackAsync();

        using var verifyScope = services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);
        var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<IRepository<NOFTenant, string>>();
        var tenant = await verifyRepo.FindAsync("tenant-1");
        Assert.Null(tenant);
    }

    [Fact]
    public async Task Transaction_NestedRollback_ShouldRestoreInnerChangesOnly()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);

        var repository = scope.ServiceProvider.GetRequiredService<IRepository<NOFTenant, string>>();
        var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await using var outer = await transactionManager.BeginTransactionAsync();
        repository.Add(new NOFTenant { Id = "outer", Name = "Outer" });
        await unitOfWork.SaveChangesAsync();

        await using var inner = await transactionManager.BeginTransactionAsync();
        repository.Add(new NOFTenant { Id = "inner", Name = "Inner" });
        await unitOfWork.SaveChangesAsync();
        await inner.RollbackAsync();
        await outer.CommitAsync();

        using var verifyScope = services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);
        var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<IRepository<NOFTenant, string>>();
        Assert.NotNull(await verifyRepo.FindAsync("outer"));
        Assert.Null(await verifyRepo.FindAsync("inner"));
    }

    [Fact]
    public async Task Transaction_DisposeWithoutCompletion_ShouldRollbackChanges()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);

        var repository = scope.ServiceProvider.GetRequiredService<IRepository<NOFTenant, string>>();
        var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await using (await transactionManager.BeginTransactionAsync())
        {
            repository.Add(new NOFTenant { Id = "tenant-dispose", Name = "Dispose" });
            await unitOfWork.SaveChangesAsync();
        }

        using var verifyScope = services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);
        var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<IRepository<NOFTenant, string>>();
        Assert.Null(await verifyRepo.FindAsync("tenant-dispose"));
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
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);

        var repository = scope.ServiceProvider.GetRequiredService<IRepository<TestOrder, long>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var order = TestOrder.Create(1, "order-1");
        order.Raise(new TestEvent("created"));
        repository.Add(order);
        await repository.FindAsync(1L);

        var changeCount = await unitOfWork.SaveChangesAsync();
        Assert.Equal(1, changeCount);
        Assert.Empty(order.Events);
    }

    [Fact]
    public async Task UnitOfWork_SaveChanges_TwiceWithoutNewChanges_ShouldReturnZero()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);

        var repository = scope.ServiceProvider.GetRequiredService<IRepository<TestOrder, long>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        repository.Add(TestOrder.Create(1, "order-1"));

        await unitOfWork.SaveChangesAsync();
        var second = await unitOfWork.SaveChangesAsync();
        Assert.Equal(0, second);
    }

    [Fact]
    public async Task InboxRepository_ShouldAddFindAndRemoveMessages()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);

        var repository = scope.ServiceProvider.GetRequiredService<IRepository<NOFInboxMessage, Guid>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var message = new NOFInboxMessage(Guid.NewGuid());

        repository.Add(message);
        await unitOfWork.SaveChangesAsync();
        Assert.NotNull(await repository.FindAsync(message.Id));

        repository.Remove(message);
        await unitOfWork.SaveChangesAsync();
        Assert.Null(await repository.FindAsync(message.Id));
    }

    [Fact]
    public async Task OutboxRepository_ShouldClaimAndMarkMessagesAsSent()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);

        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var id = Guid.NewGuid();
        repository.Add(new NOFOutboxMessage
        {
            Id = id,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            Payload = System.Text.Encoding.UTF8.GetBytes("payload"),
            Headers = "{}",
            MessageType = OutboxMessageType.Command,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await unitOfWork.SaveChangesAsync();

        var claimed = await repository.AtomicClaimPendingMessagesAsync(100).ToListAsync();
        Assert.Single(claimed);
        Assert.Equal(1, claimed[0].RetryCount);
        Assert.False(string.IsNullOrWhiteSpace(claimed[0].ClaimedBy));

        await repository.AtomicMarkAsSentAsync([claimed[0].Id]);

        using (var verify = services.CreateScope())
        {
            verify.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);
            var verifyRepo = verify.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
            var stored = await verifyRepo.FindAsync(id);
            Assert.NotNull(stored);
            Assert.Equal(OutboxMessageStatus.Sent, stored.Status);
            Assert.NotNull(stored.SentAt);
        }
    }

    [Fact]
    public async Task OutboxRepository_ShouldNotClaimExpiredOrExhaustedMessagesBeyondRules()
    {
        using var services = CreateServiceProvider(new OutboxOptions { MaxRetryCount = 2, ClaimTimeout = TimeSpan.FromMinutes(1) });
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);

        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var id1 = Guid.NewGuid();
        repository.Add(new NOFOutboxMessage
        {
            Id = id1,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            Payload = System.Text.Encoding.UTF8.GetBytes("a"),
            Headers = "{}",
            MessageType = OutboxMessageType.Command
        });

        var id2 = Guid.NewGuid();
        repository.Add(new NOFOutboxMessage
        {
            Id = id2,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            Payload = System.Text.Encoding.UTF8.GetBytes("b"),
            Headers = "{}",
            MessageType = OutboxMessageType.Command
        });
        await unitOfWork.SaveChangesAsync();

        var exhausted = await repository.FindAsync(id1);
        Assert.NotNull(exhausted);
        exhausted.RetryCount = 2;

        var claimedUntilFuture = await repository.FindAsync(id2);
        Assert.NotNull(claimedUntilFuture);
        claimedUntilFuture.ClaimedBy = "test-claim-id";
        claimedUntilFuture.ClaimExpiresAt = DateTime.UtcNow.AddMinutes(5);

        await unitOfWork.SaveChangesAsync();

        var claimed = await repository.AtomicClaimPendingMessagesAsync().ToListAsync();
        Assert.Empty(claimed);
    }

    [Fact]
    public async Task OutboxRepository_ShouldMarkMessageAsFailed_WhenRetryCountReachesLimit()
    {
        using var services = CreateServiceProvider(new OutboxOptions { MaxRetryCount = 1, ClaimTimeout = TimeSpan.FromMinutes(1) });
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);

        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var id = Guid.NewGuid();
        repository.Add(new NOFOutboxMessage
        {
            Id = id,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            Payload = System.Text.Encoding.UTF8.GetBytes("payload"),
            Headers = "{}",
            MessageType = OutboxMessageType.Notification
        });
        await unitOfWork.SaveChangesAsync();

        var claimed = await repository.AtomicClaimPendingMessagesAsync(100).ToListAsync();
        if (claimed.Count == 0)
        {
            await Task.Delay(10);
            claimed = await repository.AtomicClaimPendingMessagesAsync(100).ToListAsync();
        }
        await repository.AtomicRecordDeliveryFailureAsync(claimed[0].Id, "boom");

        using (var verify = services.CreateScope())
        {
            verify.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);
            var verifyRepo = verify.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
            var stored = await verifyRepo.FindAsync(id);
            Assert.NotNull(stored);
            Assert.Equal(OutboxMessageStatus.Failed, stored.Status);
            Assert.Equal("boom", stored.ErrorMessage);
        }
    }

    [Fact]
    public async Task StateMachineRepository_ShouldIsolateDataByTenant()
    {
        using var services = CreateServiceProvider(tenantMode: TenantMode.DatabasePerTenant);
        using (var hostScope = services.CreateScope())
        {
            var hostExecutionContext = hostScope.ServiceProvider.GetRequiredService<IExecutionContext>();
            hostExecutionContext.SetTenantId(NOFContractConstants.Tenant.HostId);
            var hostRepository = hostScope.ServiceProvider.GetRequiredService<IRepository<NOFStateMachineContext, string, string>>();
            var hostUow = hostScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            hostRepository.Add(new NOFStateMachineContext { CorrelationId = "corr", DefinitionTypeName = "def", State = 1 });
            await hostUow.SaveChangesAsync();
        }

        using (var tenantScope = services.CreateScope())
        {
            var tenantExecutionContext = tenantScope.ServiceProvider.GetRequiredService<IExecutionContext>();
            tenantExecutionContext.SetTenantId("tenant-a");
            var tenantRepository = tenantScope.ServiceProvider.GetRequiredService<IRepository<NOFStateMachineContext, string, string>>();
            var tenantUow = tenantScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            tenantRepository.Add(new NOFStateMachineContext { CorrelationId = "corr", DefinitionTypeName = "def", State = 2 });
            await tenantUow.SaveChangesAsync();
            Assert.Equal(2,
            (await tenantRepository.FindAsync("corr", "def"))!.State);
        }

        using (var verifyHostScope = services.CreateScope())
        {
            var verifyHostExecutionContext = verifyHostScope.ServiceProvider.GetRequiredService<IExecutionContext>();
            verifyHostExecutionContext.SetTenantId(NOFContractConstants.Tenant.HostId);
            var verifyHostRepository = verifyHostScope.ServiceProvider.GetRequiredService<IRepository<NOFStateMachineContext, string, string>>();
            Assert.Equal(1,
            (await verifyHostRepository.FindAsync("corr", "def"))!.State);
        }
    }

    [Fact]
    public async Task Repository_ShouldProvideQueryableTrackingAccess()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);

        var repository = scope.ServiceProvider.GetRequiredService<IRepository<TestOrder, long>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        repository.Add(TestOrder.Create(7, "before"));
        await unitOfWork.SaveChangesAsync();

        var tracked = repository.Single(order => order.Id == 7);
        tracked.Raise(new TestEvent("tracked-query"));

        var changeCount = await unitOfWork.SaveChangesAsync();

        Assert.Equal(0, changeCount);
    }

    [Fact]
    public async Task Repository_AsNoTracking_ShouldReturnDetachedReadModel()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);

        var repository = scope.ServiceProvider.GetRequiredService<IRepository<TestOrder, long>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var publisher = scope.ServiceProvider.GetRequiredService<TestEventPublisher>();

        repository.Add(TestOrder.Create(8, "before"));
        await unitOfWork.SaveChangesAsync();

        var detached = repository.AsNoTracking().Single(order => order.Id == 8);
        detached.Raise(new TestEvent("detached-query"));

        var changeCount = await unitOfWork.SaveChangesAsync();
        var stored = await repository.FindAsync(8L);

        Assert.Equal(0, changeCount);
        Assert.Equal("before", stored!.Number);
        Assert.Empty(publisher.Events);
    }

    [Fact]
    public async Task Repository_RawSql_ShouldWorkForSqliteProvider()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);

        var repository = scope.ServiceProvider.GetRequiredService<IRepository<TestOrder, long>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        repository.Add(TestOrder.Create(9, "n"));
        await unitOfWork.SaveChangesAsync();

        var rows = repository.FromSqlRaw("select * from TestOrder").ToList();
        Assert.NotEmpty(rows);

        await repository.ExecuteSqlAsync($"delete from TestOrder where Id = {9L}");
        // Query in a fresh scope to avoid first-level cache.
        using (var verify = services.CreateScope())
        {
            verify.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(NOFContractConstants.Tenant.HostId);
            var verifyRepo = verify.ServiceProvider.GetRequiredService<IRepository<TestOrder, long>>();
            Assert.Null(await verifyRepo.FindAsync(9L));
        }
    }

    private static ServiceProvider CreateServiceProvider(OutboxOptions? outboxOptions = null, TenantMode tenantMode = TenantMode.SingleTenant)
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddSingleton<TestEventPublisher>();
        builder.Services.ReplaceOrAddSingleton<IEventPublisher>(sp => sp.GetRequiredService<TestEventPublisher>());
        builder.Services.AddSingleton<HandlerInfos>();

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        builder.AddMemoryInfrastructure();

        var selector = builder.AddEFCore<TestDbContext>();
        selector = tenantMode switch
        {
            TenantMode.SharedDatabase => selector.UseSharedDatabaseTenancy(),
            TenantMode.DatabasePerTenant => selector.UseDatabasePerTenant(),
            _ => selector.UseSingleTenant()
        };

        selector.UseSqliteInMemory($"nof-tests-{Guid.NewGuid():N}");

        if (outboxOptions is not null)
        {
            builder.Services.ReplaceOrAddSingleton(Options.Create(outboxOptions));
        }

        var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        EnsureCreated(provider, NOFContractConstants.Tenant.HostId);
        EnsureCreated(provider, "tenant-a");

        return provider;
    }

    private static void EnsureCreated(ServiceProvider provider, string tenantId)
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().SetTenantId(tenantId);
        scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreated();
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

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : NOFDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestOrder>(entity =>
            {
                entity.ToTable(nameof(TestOrder));
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Number).HasMaxLength(256).IsRequired();
            });
        }
    }

    private sealed class TestServiceRegistrationContext : INOFAppBuilder
    {
        private readonly IServiceCollection _services;
        private readonly ConfigurationManager _configuration;
        private readonly IHostEnvironment _environment;
        private readonly ILoggingBuilder _logging;
        private readonly IMetricsBuilder _metrics;
        private readonly Dictionary<object, object> _properties;
        private readonly List<IServiceRegistrationStep> _registrationSteps;
        private readonly List<IApplicationInitializationStep> _initializationSteps;

        public TestServiceRegistrationContext()
        {
            _services = new ServiceCollection();
            _services.AddLogging();
            _services.AddMetrics();
            _configuration = new ConfigurationManager();
            _environment = new TestHostEnvironment();
            _logging = new TestLoggingBuilder(_services);
            _metrics = new TestMetricsBuilder(_services);
            _properties = [];
            _registrationSteps = [];
            _initializationSteps = [];
        }

        public INOFAppBuilder AddRegistrationStep<TStep>(TStep registrationStep, params Type[] allInterfaces)
            where TStep : IServiceRegistrationStep
        {
            _registrationSteps.Add(registrationStep);
            return this;
        }

        public INOFAppBuilder RemoveRegistrationStep(Predicate<IServiceRegistrationStep> predicate)
        {
            _registrationSteps.RemoveAll(predicate);
            return this;
        }

        public INOFAppBuilder AddInitializationStep<TStep>(TStep initializationStep, params Type[] allInterfaces)
            where TStep : IApplicationInitializationStep
        {
            _initializationSteps.Add(initializationStep);
            return this;
        }

        public INOFAppBuilder RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
        {
            _initializationSteps.RemoveAll(predicate);
            return this;
        }

        IServiceRegistrationContext IServiceRegistrationContext.AddInitializationStep<TStep>(TStep initializationStep, params Type[] allInterfaces)
            => AddInitializationStep(initializationStep, allInterfaces);

        IServiceRegistrationContext IServiceRegistrationContext.RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
            => RemoveInitializationStep(predicate);

        public IDictionary<object, object> Properties => _properties;

        public IConfigurationManager Configuration => _configuration;

        public IHostEnvironment Environment => _environment;

        public ILoggingBuilder Logging => _logging;

        public IMetricsBuilder Metrics => _metrics;

        public IServiceCollection Services => _services;

        public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
            where TContainerBuilder : notnull
        {
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "NOF.Infrastructure.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestLoggingBuilder(IServiceCollection services) : ILoggingBuilder
    {
        public IServiceCollection Services { get; } = services;
    }

    private sealed class TestMetricsBuilder(IServiceCollection services) : IMetricsBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
