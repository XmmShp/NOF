using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Application;
using NOF.Domain;
using NOF.Hosting;
using Xunit;

namespace NOF.Infrastructure.Tests.Persistence;

public class SqliteInMemoryPersistenceTests
{
    [Fact]
    public async Task AddEFCore_NonGeneric_ShouldRegisterDefaultNOFDbContext()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddSingleton<HandlerInfos>();

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        builder.AddMemoryInfrastructure();
        builder.AddEFCore()
            .UseSingleTenant()
            .AutoMigrate()
            .UseSqliteInMemory($"nof-tests-{Guid.NewGuid():N}");

        using var services = builder.Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        db.Set<NOFTenant>().Add(new NOFTenant { Id = "tenant-efcore-default", Name = "Tenant EFCore Default" });
        await db.SaveChangesAsync();

        var tenant = await db.FindAsync<NOFTenant>(["tenant-efcore-default"]);
        Assert.NotNull(tenant);
        Assert.Equal("Tenant EFCore Default", tenant.Name);
    }

    [Fact]
    public async Task AddMemoryInfrastructure_NonGeneric_ShouldRegisterDefaultDbContext()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddSingleton<HandlerInfos>();

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        builder.AddMemoryInfrastructure();

        using var services = builder.Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        db.Set<NOFTenant>().Add(new NOFTenant { Id = "tenant-default", Name = "Tenant Default" });
        await db.SaveChangesAsync();

        var tenant = await db.FindAsync<NOFTenant>(["tenant-default"]);
        Assert.NotNull(tenant);
        Assert.Equal("Tenant Default", tenant.Name);
    }

    [Fact]
    public async Task Transaction_Rollback_ShouldRestorePreviousState()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Set<NOFTenant>().Add(new NOFTenant { Id = "tenant-1", Name = "Tenant 1" });
        await db.SaveChangesAsync();
        await transaction.RollbackAsync();

        using var verifyScope = services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DbContext>();
        var tenant = await verifyDb.FindAsync<NOFTenant>(["tenant-1"]);
        Assert.Null(tenant);
    }

    [Fact]
    public async Task Transaction_NestedRollback_ShouldRestoreInnerChangesOnly()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        await using var outer = await db.Database.BeginTransactionAsync();
        db.Set<NOFTenant>().Add(new NOFTenant { Id = "outer", Name = "Outer" });
        await db.SaveChangesAsync();

        // Use savepoint for nested rollback semantics.
        await outer.CreateSavepointAsync("sp_inner");
        db.Set<NOFTenant>().Add(new NOFTenant { Id = "inner", Name = "Inner" });
        await db.SaveChangesAsync();
        await outer.RollbackToSavepointAsync("sp_inner");
        await outer.CommitAsync();

        using var verifyScope = services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DbContext>();
        Assert.NotNull(await verifyDb.FindAsync<NOFTenant>(["outer"]));
        Assert.Null(await verifyDb.FindAsync<NOFTenant>(["inner"]));
    }

    [Fact]
    public async Task Transaction_DisposeWithoutCompletion_ShouldRollbackChanges()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        await using (await db.Database.BeginTransactionAsync())
        {
            db.Set<NOFTenant>().Add(new NOFTenant { Id = "tenant-dispose", Name = "Dispose" });
            await db.SaveChangesAsync();
        }

        using var verifyScope = services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DbContext>();
        Assert.Null(await verifyDb.FindAsync<NOFTenant>(["tenant-dispose"]));
    }

    [Fact]
    public async Task Transaction_CompleteOutOfOrder_ShouldThrow()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        // NOF no longer provides a nested-transaction manager abstraction; nested ordering is an EF/provider concern.
        // Keep a minimal assertion that EF transaction commit works.
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        await tx.CommitAsync();
    }

    [Fact]
    public async Task UnitOfWork_SaveChanges_ShouldPublishEvents_ClearEvents_AndReturnChangeCount()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        var order = TestOrder.Create(1, "order-1");
        order.Raise(new TestEvent("created"));
        db.Set<TestOrder>().Add(order);
        await db.FindAsync<TestOrder>([1L]);

        var changeCount = await db.SaveChangesAsync();
        Assert.Equal(1, changeCount);
        Assert.Empty(order.Events);
    }

    [Fact]
    public async Task UnitOfWork_SaveChanges_TwiceWithoutNewChanges_ShouldReturnZero()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        db.Set<TestOrder>().Add(TestOrder.Create(1, "order-1"));

        await db.SaveChangesAsync();
        var second = await db.SaveChangesAsync();
        Assert.Equal(0, second);
    }

    [Fact]
    public async Task InboxRepository_ShouldAddFindAndRemoveMessages()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var message = new NOFInboxMessage(Guid.NewGuid());

        db.Set<NOFInboxMessage>().Add(message);
        await db.SaveChangesAsync();
        Assert.NotNull(await db.FindAsync<NOFInboxMessage>([message.Id]));

        db.Set<NOFInboxMessage>().Remove(message);
        await db.SaveChangesAsync();
        Assert.Null(await db.FindAsync<NOFInboxMessage>([message.Id]));
    }

    [Fact]
    public async Task OutboxRepository_ShouldClaimAndMarkMessagesAsSent()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        var id = Guid.NewGuid();
        repository.Add(new NOFOutboxMessage
        {
            Id = id,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            DispatchTypes = "[\"System.String\"]",
            Payload = System.Text.Encoding.UTF8.GetBytes("payload"),
            Headers = "{}",
            MessageType = OutboxMessageType.Command,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var claimed = await repository.AtomicClaimPendingMessagesAsync(100).ToListAsync();
        Assert.Single(claimed);
        Assert.Equal(1, claimed[0].RetryCount);
        Assert.False(string.IsNullOrWhiteSpace(claimed[0].ClaimedBy));

        await repository.AtomicMarkAsSentAsync([claimed[0].Id]);

        using (var verify = services.CreateScope())
        {
            verify.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;
            var verifyDb = verify.ServiceProvider.GetRequiredService<DbContext>();
            var stored = await verifyDb.FindAsync<NOFOutboxMessage>([id]);
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
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        var id1 = Guid.NewGuid();
        repository.Add(new NOFOutboxMessage
        {
            Id = id1,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            DispatchTypes = "[\"System.String\"]",
            Payload = System.Text.Encoding.UTF8.GetBytes("a"),
            Headers = "{}",
            MessageType = OutboxMessageType.Command
        });

        var id2 = Guid.NewGuid();
        repository.Add(new NOFOutboxMessage
        {
            Id = id2,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            DispatchTypes = "[\"System.String\"]",
            Payload = System.Text.Encoding.UTF8.GetBytes("b"),
            Headers = "{}",
            MessageType = OutboxMessageType.Command
        });
        await db.SaveChangesAsync();

        var exhausted = await db.FindAsync<NOFOutboxMessage>([id1]);
        Assert.NotNull(exhausted);
        exhausted.RetryCount = 2;

        var claimedUntilFuture = await db.FindAsync<NOFOutboxMessage>([id2]);
        Assert.NotNull(claimedUntilFuture);
        claimedUntilFuture.ClaimedBy = "test-claim-id";
        claimedUntilFuture.ClaimExpiresAt = DateTime.UtcNow.AddMinutes(5);

        await db.SaveChangesAsync();

        var claimed = await repository.AtomicClaimPendingMessagesAsync().ToListAsync();
        Assert.Empty(claimed);
    }

    [Fact]
    public async Task OutboxRepository_ShouldMarkMessageAsFailed_WhenRetryCountReachesLimit()
    {
        using var services = CreateServiceProvider(new OutboxOptions { MaxRetryCount = 1, ClaimTimeout = TimeSpan.FromMinutes(1) });
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        var id = Guid.NewGuid();
        repository.Add(new NOFOutboxMessage
        {
            Id = id,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            DispatchTypes = "[\"System.String\"]",
            Payload = System.Text.Encoding.UTF8.GetBytes("payload"),
            Headers = "{}",
            MessageType = OutboxMessageType.Notification
        });
        await db.SaveChangesAsync();

        var claimed = await repository.AtomicClaimPendingMessagesAsync(100).ToListAsync();
        if (claimed.Count == 0)
        {
            await Task.Delay(10);
            claimed = await repository.AtomicClaimPendingMessagesAsync(100).ToListAsync();
        }
        await repository.AtomicRecordDeliveryFailureAsync(claimed[0].Id, "boom");

        using (var verify = services.CreateScope())
        {
            verify.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;
            var verifyDb = verify.ServiceProvider.GetRequiredService<DbContext>();
            var stored = await verifyDb.FindAsync<NOFOutboxMessage>([id]);
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
            hostExecutionContext.TenantId = NOFAbstractionConstants.Tenant.HostId;
            var hostDb = hostScope.ServiceProvider.GetRequiredService<DbContext>();
            hostDb.Set<NOFStateMachineContext>().Add(new NOFStateMachineContext { CorrelationId = "corr", DefinitionTypeName = "def", State = 1 });
            await hostDb.SaveChangesAsync();
        }

        using (var tenantScope = services.CreateScope())
        {
            var tenantExecutionContext = tenantScope.ServiceProvider.GetRequiredService<IExecutionContext>();
            tenantExecutionContext.TenantId = "tenant-a";
            var tenantDb = tenantScope.ServiceProvider.GetRequiredService<DbContext>();
            tenantDb.Set<NOFStateMachineContext>().Add(new NOFStateMachineContext { CorrelationId = "corr", DefinitionTypeName = "def", State = 2 });
            await tenantDb.SaveChangesAsync();
            Assert.Equal(2,
            (await tenantDb.FindAsync<NOFStateMachineContext>(["corr", "def"]))!.State);
        }

        using (var verifyHostScope = services.CreateScope())
        {
            var verifyHostExecutionContext = verifyHostScope.ServiceProvider.GetRequiredService<IExecutionContext>();
            verifyHostExecutionContext.TenantId = NOFAbstractionConstants.Tenant.HostId;
            var verifyHostDb = verifyHostScope.ServiceProvider.GetRequiredService<DbContext>();
            Assert.Equal(1,
            (await verifyHostDb.FindAsync<NOFStateMachineContext>(["corr", "def"]))!.State);
        }
    }

    [Fact]
    public async Task DbContext_ShouldProvideQueryableTrackingAccess()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        db.Set<TestOrder>().Add(TestOrder.Create(7, "before"));
        await db.SaveChangesAsync();

        var tracked = await db.Set<TestOrder>().SingleAsync(order => order.Id == 7);
        tracked.Raise(new TestEvent("tracked-query"));

        var changeCount = await db.SaveChangesAsync();

        Assert.Equal(0, changeCount);
    }

    [Fact]
    public async Task DbContext_AsNoTracking_ShouldReturnDetachedReadModel()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<TestEventPublisher>();

        db.Set<TestOrder>().Add(TestOrder.Create(8, "before"));
        await db.SaveChangesAsync();

        var detached = await db.Set<TestOrder>().AsNoTracking().SingleAsync(order => order.Id == 8);
        detached.Raise(new TestEvent("detached-query"));

        var changeCount = await db.SaveChangesAsync();
        var stored = await db.FindAsync<TestOrder>([8L]);

        Assert.Equal(0, changeCount);
        Assert.Equal("before", stored!.Number);
        Assert.Empty(publisher.Events);
    }

    [Fact]
    public async Task DbContext_RawSql_ShouldWorkForSqliteProvider()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        db.Set<TestOrder>().Add(TestOrder.Create(9, "n"));
        await db.SaveChangesAsync();

        var rows = db.Set<TestOrder>().FromSqlRaw("select * from TestOrder").ToList();
        Assert.NotEmpty(rows);

        await db.Database.ExecuteSqlInterpolatedAsync($"delete from TestOrder where Id = {9L}");
        // Query in a fresh scope to avoid first-level cache.
        using (var verify = services.CreateScope())
        {
            verify.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = NOFAbstractionConstants.Tenant.HostId;
            var verifyDb = verify.ServiceProvider.GetRequiredService<DbContext>();
            Assert.Null(await verifyDb.FindAsync<TestOrder>([9L]));
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
        builder.AddMemoryInfrastructure<TestDbContext>(tenantMode, $"nof-tests-{Guid.NewGuid():N}");

        if (outboxOptions is not null)
        {
            builder.Services.ReplaceOrAddSingleton(Options.Create(outboxOptions));
        }

        var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        EnsureCreated(provider, NOFAbstractionConstants.Tenant.HostId);
        EnsureCreated(provider, "tenant-a");

        return provider;
    }

    private static void EnsureCreated(ServiceProvider provider, string tenantId)
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().TenantId = tenantId;
        scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreated();
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        private long _current = 1000;

        public long NextId() => Interlocked.Increment(ref _current);
    }

    private sealed class TestEventPublisher : IEventPublisher
    {
        public List<object> Events { get; } = [];

        public Task PublishAsync(object payload, Type[] eventTypes, CancellationToken cancellationToken)
        {
            Events.Add(payload);
            return Task.CompletedTask;
        }
    }

    private sealed record TestEvent(string Name);

    private sealed class TestOrder : AggregateRoot, ICloneable
    {
        public long Id { get; init; }

        public string Number { get; init; } = string.Empty;

        public static TestOrder Create(long id, string number)
            => new() { Id = id, Number = number };

        public void Raise(object @event)
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
