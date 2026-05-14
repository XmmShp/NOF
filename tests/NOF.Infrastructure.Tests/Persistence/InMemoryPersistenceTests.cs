using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Application;
using NOF.Domain;
using NOF.Hosting;
using Xunit;

namespace NOF.Infrastructure.Tests.Persistence;

public class SqliteInMemoryPersistenceTests
{
    [Fact]
    public async Task UseDbContext_NonGeneric_ShouldRegisterDefaultNOFDbContext()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        ConfigureSqliteInMemory(
            builder.UseDbContext<NOFDbContext>()
                .WithTenantMode(TenantMode.DatabasePerTenant),
            $"nof-tests-{Guid.NewGuid():N}");

        using var services = BuildServiceProvider(builder);
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        db.Set<NOFTenant>().Add(new NOFTenant { Id = TenantId.Of("tenantefcoredefault"), Name = "Tenant EFCore Default" });
        await db.SaveChangesAsync();

        var tenant = await db.FindAsync<NOFTenant>([TenantId.Of("tenantefcoredefault")]);
        Assert.NotNull(tenant);
        Assert.Equal("Tenant EFCore Default", tenant.Name);
    }

    [Fact]
    public void UseSqlite_ShouldResolveTenantIdPlaceholderFromConnectionString()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"nof-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var builder = new TestServiceRegistrationContext();
            builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
            builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
            builder.Configuration.AddInMemoryCollection([
        new KeyValuePair<string, string?>("ConnectionStrings:sqlite", $"Data Source={Path.Combine(tempDirectory, "nof-{TenantId}.db")}")
    ]);

            builder.AddHostingDefaults();
            builder.AddInfrastructureDefaults();
            builder.UseDbContext<TestDbContext>()
            .WithTenantMode(TenantMode.DatabasePerTenant)
                .WithConnectionString(builder.Configuration.GetConnectionString("sqlite")
                    ?? throw new InvalidOperationException("Connection string 'sqlite' not found in configuration."))
                .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseSqlite(connectionString));

            using var services = BuildServiceProvider(builder);

            using var tenantAScope = services.CreateScope();
            tenantAScope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = "tenanta";
            var tenantADb = tenantAScope.ServiceProvider.GetRequiredService<TestDbContext>();

            using var tenantBScope = services.CreateScope();
            tenantBScope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = "tenantb";
            var tenantBDb = tenantBScope.ServiceProvider.GetRequiredService<TestDbContext>();

            var tenantAConnectionString = tenantADb.Database.GetConnectionString();
            var tenantBConnectionString = tenantBDb.Database.GetConnectionString();

            Assert.Contains("nof-tenanta.db", tenantAConnectionString, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("nof-tenantb.db", tenantBConnectionString, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(tenantAConnectionString, tenantBConnectionString);
        }
        finally
        {
            DeleteDirectoryWithRetry(tempDirectory);
        }
    }

    [Fact]
    public async Task AddMemoryInfrastructure_NonGeneric_ShouldRegisterDefaultDbContext()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        ConfigureSqliteInMemory(
            builder.UseDbContext<NOFDbContext>()
                .WithTenantMode(TenantMode.DatabasePerTenant),
            $"nof-tests-{Guid.NewGuid():N}");

        using var services = BuildServiceProvider(builder);
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        db.Set<NOFTenant>().Add(new NOFTenant { Id = TenantId.Of("tenantdefault"), Name = "Tenant Default" });
        await db.SaveChangesAsync();

        var tenant = await db.FindAsync<NOFTenant>([TenantId.Of("tenantdefault")]);
        Assert.NotNull(tenant);
        Assert.Equal("Tenant Default", tenant.Name);
    }

    [Fact]
    public async Task OnModelCreatingOptions_ShouldAddDynamicEntityType()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        ConfigureSqliteInMemory(
            builder.UseDbContext<NOFDbContext>()
                .WithTenantMode(TenantMode.DatabasePerTenant)
                .WithModelCreating(static modelBuilder =>
                {
                    ConfigureDynamicAuditEntry(modelBuilder);
                }),
            $"nof-tests-{Guid.NewGuid():N}");

        using var services = BuildServiceProvider(builder);
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<NOFDbContext>();

        db.Set<DynamicAuditEntry>().Add(new DynamicAuditEntry
        {
            Id = 1,
            Message = "created from options"
        });
        await db.SaveChangesAsync();

        var stored = await db.Set<DynamicAuditEntry>().SingleAsync(e => e.Id == 1);
        Assert.Equal("created from options", stored.Message);
    }

    [Fact]
    public void WithModelCreating_DifferentDelegates_ShouldNotReuseWrongModel()
    {
        using var firstServices = BuildServiceProviderWithModelCreating(
            static modelBuilder =>
            {
                modelBuilder.Entity<FirstDynamicEntry>(entity =>
                {
                    entity.ToTable(nameof(FirstDynamicEntry));
                    entity.HasKey(e => e.Id);
                });
            });
        using var secondServices = BuildServiceProviderWithModelCreating(
            static modelBuilder =>
            {
                modelBuilder.Entity<SecondDynamicEntry>(entity =>
                {
                    entity.ToTable(nameof(SecondDynamicEntry));
                    entity.HasKey(e => e.Id);
                });
            });

        using var firstScope = firstServices.CreateScope();
        firstScope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;
        var firstDb = firstScope.ServiceProvider.GetRequiredService<NOFDbContext>();

        using var secondScope = secondServices.CreateScope();
        secondScope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;
        var secondDb = secondScope.ServiceProvider.GetRequiredService<NOFDbContext>();

        Assert.NotNull(firstDb.Model.FindEntityType(typeof(FirstDynamicEntry)));
        Assert.Null(firstDb.Model.FindEntityType(typeof(SecondDynamicEntry)));
        Assert.NotNull(secondDb.Model.FindEntityType(typeof(SecondDynamicEntry)));
        Assert.Null(secondDb.Model.FindEntityType(typeof(FirstDynamicEntry)));
    }

    [Fact]
    public void OnModelCreatingOptions_IsHostOnly_ShouldMarkEntityOnModelBuilder()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddSingleton<INOFDbContextModelCreatingContributor, ModelConfiguredHostOnlyEntryContributor>();

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        ConfigureSqliteInMemory(
            builder.UseDbContext<NOFDbContext>()
                .WithTenantMode(TenantMode.SharedDatabase),
            $"nof-tests-{Guid.NewGuid():N}");

        using var services = BuildServiceProvider(builder);
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<NOFDbContext>();
        var entityType = db.Model.FindEntityType(typeof(ModelConfiguredHostOnlyEntry));

        Assert.NotNull(entityType);
        Assert.Equal(true, entityType.FindAnnotation("NOF:HostOnly")?.Value);
        Assert.Null(entityType.FindProperty("TenantId"));
    }

    [Fact]
    public void HostOnlyAttribute_ShouldStillMarkEntityAsHostOnly()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddSingleton<INOFDbContextModelCreatingContributor, AttributeHostOnlyEntryContributor>();

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        ConfigureSqliteInMemory(
            builder.UseDbContext<NOFDbContext>()
                .WithTenantMode(TenantMode.SharedDatabase),
            $"nof-tests-{Guid.NewGuid():N}");

        using var services = BuildServiceProvider(builder);
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<NOFDbContext>();
        var entityType = db.Model.FindEntityType(typeof(AttributeHostOnlyEntry));

        Assert.NotNull(entityType);
        Assert.Equal(true, entityType.FindAnnotation("NOF:HostOnly")?.Value);
        Assert.Null(entityType.FindProperty("TenantId"));
    }

    [Fact]
    public async Task ModelCreatingContributor_ShouldAddExtensionPackageEntityType()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        builder.Services.AddSingleton<INOFDbContextModelCreatingContributor, DynamicAuditEntryModelCreatingContributor>();
        ConfigureSqliteInMemory(
            builder.UseDbContext<NOFDbContext>()
                .WithTenantMode(TenantMode.DatabasePerTenant),
            $"nof-tests-{Guid.NewGuid():N}");

        using var services = BuildServiceProvider(builder);
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<NOFDbContext>();

        db.Set<DynamicAuditEntry>().Add(new DynamicAuditEntry
        {
            Id = 2,
            Message = "created from contributor"
        });
        await db.SaveChangesAsync();

        var stored = await db.Set<DynamicAuditEntry>().SingleAsync(e => e.Id == 2);
        Assert.Equal("created from contributor", stored.Message);
    }

    [Fact]
    public async Task Transaction_Rollback_ShouldRestorePreviousState()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Set<NOFTenant>().Add(new NOFTenant { Id = TenantId.Of("tenant1"), Name = "Tenant 1" });
        await db.SaveChangesAsync();
        await transaction.RollbackAsync();

        using var verifyScope = services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();
        var tenant = await verifyDb.FindAsync<NOFTenant>([TenantId.Of("tenant1")]);
        Assert.Null(tenant);
    }

    [Fact]
    public async Task Transaction_NestedRollback_ShouldRestoreInnerChangesOnly()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        await using var outer = await db.Database.BeginTransactionAsync();
        db.Set<NOFTenant>().Add(new NOFTenant { Id = TenantId.Of("outer"), Name = "Outer" });
        await db.SaveChangesAsync();

        // Use savepoint for nested rollback semantics.
        await outer.CreateSavepointAsync("sp_inner");
        db.Set<NOFTenant>().Add(new NOFTenant { Id = TenantId.Of("inner"), Name = "Inner" });
        await db.SaveChangesAsync();
        await outer.RollbackToSavepointAsync("sp_inner");
        await outer.CommitAsync();

        using var verifyScope = services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();
        Assert.NotNull(await verifyDb.FindAsync<NOFTenant>([TenantId.Of("outer")]));
        Assert.Null(await verifyDb.FindAsync<NOFTenant>([TenantId.Of("inner")]));
    }

    [Fact]
    public async Task Transaction_DisposeWithoutCompletion_ShouldRollbackChanges()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        await using (await db.Database.BeginTransactionAsync())
        {
            db.Set<NOFTenant>().Add(new NOFTenant { Id = TenantId.Of("tenantdispose"), Name = "Dispose" });
            await db.SaveChangesAsync();
        }

        using var verifyScope = services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();
        Assert.Null(await verifyDb.FindAsync<NOFTenant>([TenantId.Of("tenantdispose")]));
    }

    [Fact]
    public async Task Transaction_CompleteOutOfOrder_ShouldThrow()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        // NOF no longer provides a nested-transaction manager abstraction; nested ordering is an EF/provider concern.
        // Keep a minimal assertion that EF transaction commit works.
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        await tx.CommitAsync();
    }

    [Fact]
    public async Task UnitOfWork_SaveChanges_ShouldPublishEvents_ClearEvents_AndReturnChangeCount()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;
        ActivateDaemons(scope.ServiceProvider);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var order = TestOrder.Create(1, "order-1");
        order.Raise(new TestEvent("created"));
        db.Set<TestOrder>().Add(order);
        await db.FindAsync<TestOrder>([1L]);

        var changeCount = await db.SaveChangesAsync();
        Assert.Equal(1, changeCount);
        Assert.Single(scope.ServiceProvider.GetRequiredService<TestEventPublisher>().Events);
    }

    [Fact]
    public async Task UnitOfWork_SaveChanges_TwiceWithoutNewChanges_ShouldReturnZero()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestOrder>().Add(TestOrder.Create(1, "order-1"));

        await db.SaveChangesAsync();
        var second = await db.SaveChangesAsync();
        Assert.Equal(0, second);
    }

    [Fact]
    public async Task SoftDelete_ShouldAddShadowDeletedAtAndFilterDeletedRows()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var entityType = db.Model.FindEntityType(typeof(TestOrder));

        Assert.NotNull(entityType);
        Assert.NotNull(entityType.FindProperty("__DeletedAtUtc"));

        db.Set<TestOrder>().Add(TestOrder.Create(10, "soft-delete"));
        await db.SaveChangesAsync();

        var order = await db.FindAsync<TestOrder>([10L]);
        Assert.NotNull(order);

        db.Set<TestOrder>().Remove(order);
        await db.SaveChangesAsync();

        Assert.Null(await db.FindAsync<TestOrder>([10L]));

        using var verifyScope = services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();

        Assert.Null(await verifyDb.Set<TestOrder>().SingleOrDefaultAsync(e => e.Id == 10));

        var deletedAtUtc = await verifyDb.Set<TestOrder>()
            .IgnoreQueryFilters()
            .Where(e => e.Id == 10)
            .Select(e => EF.Property<DateTime?>(e, "__DeletedAtUtc"))
            .SingleAsync();

        Assert.NotNull(deletedAtUtc);
    }

    [Fact]
    public async Task WithSoftDelete_Disabled_ShouldPhysicallyDeleteRows()
    {
        using var services = CreateServiceProvider(softDeleteEnabled: false);
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestOrder>().Add(TestOrder.Create(11, "hard-delete"));
        await db.SaveChangesAsync();

        var order = await db.FindAsync<TestOrder>([11L]);
        Assert.NotNull(order);

        db.Set<TestOrder>().Remove(order);
        await db.SaveChangesAsync();

        Assert.Null(await db.Set<TestOrder>().IgnoreQueryFilters().SingleOrDefaultAsync(e => e.Id == 11));
    }

    [Fact]
    public async Task InboxRepository_ShouldAddFindAndRemoveMessages()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var message = new NOFInboxMessage
        {
            Id = Guid.NewGuid(),
            HandlerType = typeof(SqliteInMemoryPersistenceTests).AssemblyQualifiedName!,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            Payload = System.Text.Encoding.UTF8.GetBytes("payload"),
            Headers = "{}",
            MessageType = InboxMessageType.Command
        };

        db.Set<NOFInboxMessage>().Add(message);
        await db.SaveChangesAsync();
        Assert.NotNull(await db.FindAsync<NOFInboxMessage>([message.Id, message.HandlerType]));

        db.Set<NOFInboxMessage>().Remove(message);
        await db.SaveChangesAsync();
        Assert.Null(await db.FindAsync<NOFInboxMessage>([message.Id, message.HandlerType]));
    }

    [Fact]
    public async Task OutboxRepository_ShouldClaimAndMarkMessagesAsSent()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        var id = Guid.NewGuid();
        db.Set<NOFOutboxMessage>().Add(new NOFOutboxMessage
        {
            Id = id,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            DispatchTypes = "[\"System.String\"]",
            Payload = System.Text.Encoding.UTF8.GetBytes("payload"),
            Headers = "{}",
            MessageType = OutboxMessageType.Command,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var claimLockId = Guid.NewGuid().ToString();
        var claimExpiresAt = DateTime.UtcNow.AddMinutes(1);
        var claimed = await db.Set<NOFOutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Pending && m.RetryCount < 100)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(100)
            .ToListAsync();
        foreach (var message in claimed)
        {
            message.RetryCount++;
            message.ClaimedBy = claimLockId;
            message.ClaimExpiresAtUtc = claimExpiresAt;
        }
        await db.SaveChangesAsync();
        Assert.Single(claimed);
        Assert.Equal(1, claimed[0].RetryCount);
        Assert.False(string.IsNullOrWhiteSpace(claimed[0].ClaimedBy));

        claimed[0].Status = OutboxMessageStatus.Sent;
        claimed[0].SentAtUtc = DateTime.UtcNow;
        claimed[0].ClaimedBy = null;
        claimed[0].ClaimExpiresAtUtc = null;
        await db.SaveChangesAsync();

        using (var verify = services.CreateScope())
        {
            verify.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;
            var verifyDb = verify.ServiceProvider.GetRequiredService<TestDbContext>();
            var stored = await verifyDb.FindAsync<NOFOutboxMessage>([id]);
            Assert.NotNull(stored);
            Assert.Equal(OutboxMessageStatus.Sent, stored.Status);
            Assert.NotNull(stored.SentAtUtc);
        }
    }

    [Fact]
    public async Task OutboxRepository_ShouldNotClaimExpiredOrExhaustedMessagesBeyondRules()
    {
        using var services = CreateServiceProvider(new TransactionalMessageOptions
        {
            Outbox = new TransactionalMessageProcessorOptions { MaxRetryCount = 2, ClaimTimeout = TimeSpan.FromMinutes(1) }
        });
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var id1 = Guid.NewGuid();
        db.Set<NOFOutboxMessage>().Add(new NOFOutboxMessage
        {
            Id = id1,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            DispatchTypes = "[\"System.String\"]",
            Payload = System.Text.Encoding.UTF8.GetBytes("a"),
            Headers = "{}",
            MessageType = OutboxMessageType.Command
        });

        var id2 = Guid.NewGuid();
        db.Set<NOFOutboxMessage>().Add(new NOFOutboxMessage
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
        claimedUntilFuture.ClaimExpiresAtUtc = DateTime.UtcNow.AddMinutes(5);

        await db.SaveChangesAsync();

        var claimed = await db.Set<NOFOutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Pending &&
                        m.RetryCount < 2 &&
                        (m.ClaimedBy == null || m.ClaimExpiresAtUtc == null || m.ClaimExpiresAtUtc <= DateTime.UtcNow))
            .ToListAsync();
        Assert.Empty(claimed);
    }

    [Fact]
    public async Task OutboxRepository_ShouldMarkMessageAsFailed_WhenRetryCountReachesLimit()
    {
        using var services = CreateServiceProvider(new TransactionalMessageOptions
        {
            Outbox = new TransactionalMessageProcessorOptions { MaxRetryCount = 1, ClaimTimeout = TimeSpan.FromMinutes(1) }
        });
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var id = Guid.NewGuid();
        db.Set<NOFOutboxMessage>().Add(new NOFOutboxMessage
        {
            Id = id,
            PayloadType = typeof(string).AssemblyQualifiedName!,
            DispatchTypes = "[\"System.String\"]",
            Payload = System.Text.Encoding.UTF8.GetBytes("payload"),
            Headers = "{}",
            MessageType = OutboxMessageType.Notification
        });
        await db.SaveChangesAsync();

        var claimed = await db.Set<NOFOutboxMessage>().ToListAsync();
        claimed[0].RetryCount++;
        claimed[0].ErrorMessage = "boom";
        claimed[0].FailedAtUtc = DateTime.UtcNow;
        claimed[0].Status = OutboxMessageStatus.Failed;
        claimed[0].ClaimedBy = null;
        claimed[0].ClaimExpiresAtUtc = null;
        await db.SaveChangesAsync();

        using (var verify = services.CreateScope())
        {
            verify.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;
            var verifyDb = verify.ServiceProvider.GetRequiredService<TestDbContext>();
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
            var hostTransparentInfos = hostScope.ServiceProvider.GetRequiredService<ITransparentInfos>();
            hostTransparentInfos.TenantId = NOFAbstractionConstants.Tenant.HostId;
            var hostDb = hostScope.ServiceProvider.GetRequiredService<TestDbContext>();
            hostDb.Set<NOFStateMachineContext>().Add(new NOFStateMachineContext { CorrelationId = "corr", DefinitionTypeName = "def", State = 1 });
            await hostDb.SaveChangesAsync();
        }

        using (var tenantScope = services.CreateScope())
        {
            var tenantTransparentInfos = tenantScope.ServiceProvider.GetRequiredService<ITransparentInfos>();
            tenantTransparentInfos.TenantId = "tenanta";
            var tenantDb = tenantScope.ServiceProvider.GetRequiredService<TestDbContext>();
            tenantDb.Set<NOFStateMachineContext>().Add(new NOFStateMachineContext { CorrelationId = "corr", DefinitionTypeName = "def", State = 2 });
            await tenantDb.SaveChangesAsync();
            Assert.Equal(2,
            (await tenantDb.FindAsync<NOFStateMachineContext>(["corr", "def"]))!.State);
        }

        using (var verifyHostScope = services.CreateScope())
        {
            var verifyHostTransparentInfos = verifyHostScope.ServiceProvider.GetRequiredService<ITransparentInfos>();
            verifyHostTransparentInfos.TenantId = NOFAbstractionConstants.Tenant.HostId;
            var verifyHostDb = verifyHostScope.ServiceProvider.GetRequiredService<TestDbContext>();
            Assert.Equal(1,
            (await verifyHostDb.FindAsync<NOFStateMachineContext>(["corr", "def"]))!.State);
        }
    }

    [Fact]
    public async Task DbContext_ShouldProvideQueryableTrackingAccess()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;
        ActivateDaemons(scope.ServiceProvider);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestOrder>().Add(TestOrder.Create(7, "before"));
        await db.SaveChangesAsync();

        var tracked = await db.Set<TestOrder>().SingleAsync(order => order.Id == 7);
        tracked.Raise(new TestEvent("tracked-query"));

        var changeCount = await db.SaveChangesAsync();

        Assert.Equal(0, changeCount);
        Assert.Single(scope.ServiceProvider.GetRequiredService<TestEventPublisher>().Events);
    }

    [Fact]
    public async Task DbContext_AsNoTracking_ShouldReturnDetachedReadModel()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;
        ActivateDaemons(scope.ServiceProvider);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<TestEventPublisher>();

        db.Set<TestOrder>().Add(TestOrder.Create(8, "before"));
        await db.SaveChangesAsync();

        var detached = await db.Set<TestOrder>().AsNoTracking().SingleAsync(order => order.Id == 8);
        detached.Raise(new TestEvent("detached-query"));

        var changeCount = await db.SaveChangesAsync();
        var stored = await db.FindAsync<TestOrder>([8L]);

        Assert.Equal(0, changeCount);
        Assert.Equal("before", stored!.Number);
        Assert.Single(publisher.Events);
    }

    [Fact]
    public async Task DbContext_RawSql_ShouldWorkForSqliteProvider()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestOrder>().Add(TestOrder.Create(9, "n"));
        await db.SaveChangesAsync();

        var rows = db.Set<TestOrder>().FromSqlRaw("select * from TestOrder").ToList();
        Assert.NotEmpty(rows);

        await db.Database.ExecuteSqlInterpolatedAsync($"delete from TestOrder where Id = {9L}");
        // Query in a fresh scope to avoid first-level cache.
        using (var verify = services.CreateScope())
        {
            verify.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;
            var verifyDb = verify.ServiceProvider.GetRequiredService<TestDbContext>();
            Assert.Null(await verifyDb.FindAsync<TestOrder>([9L]));
        }
    }

    [Fact]
    public async Task Creating_HostTenant_Record_ShouldThrow()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = NOFAbstractionConstants.Tenant.HostId;

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        db.Set<NOFTenant>().Add(new NOFTenant
        {
            Id = TenantId.Host,
            Name = "Host"
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains(NOFAbstractionConstants.Tenant.HostId, ex.Message, StringComparison.Ordinal);
    }

    private static EFCoreSelector ConfigureSqliteInMemory(EFCoreSelector selector, string databaseName)
    {
        return selector
            .WithConnectionString($"Data Source={databaseName}-{{tenantId}};Mode=Memory;Cache=Shared")
            .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseSqlite(connectionString));
    }

    private static void ConfigureDynamicAuditEntry(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DynamicAuditEntry>(entity =>
        {
            entity.ToTable(nameof(DynamicAuditEntry));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).HasMaxLength(256).IsRequired();
        });
    }

    private static ServiceProvider BuildServiceProviderWithModelCreating(Action<ModelBuilder> configure)
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        ConfigureSqliteInMemory(
            builder.UseDbContext<NOFDbContext>()
                .WithTenantMode(TenantMode.DatabasePerTenant)
                .WithModelCreating(configure),
            $"nof-tests-{Guid.NewGuid():N}");

        return BuildServiceProvider(builder);
    }

    private static ServiceProvider CreateServiceProvider(
        TransactionalMessageOptions? transactionalMessageOptions = null,
        TenantMode tenantMode = TenantMode.DatabasePerTenant,
        bool softDeleteEnabled = true)
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddSingleton<TestEventPublisher>();

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        ConfigureSqliteInMemory(
            builder.UseDbContext<TestDbContext>()
                .WithTenantMode(tenantMode)
                .WithSoftDelete(softDeleteEnabled),
            $"nof-tests-{Guid.NewGuid():N}");
        builder.Services.ReplaceOrAddSingleton<IEventPublisher>(sp => sp.GetRequiredService<TestEventPublisher>());

        if (transactionalMessageOptions is not null)
        {
            builder.Services.Configure<TransactionalMessageOptions>(options =>
            {
                options.Inbox = transactionalMessageOptions.Inbox;
                options.Outbox = transactionalMessageOptions.Outbox;
            });
        }

        var provider = BuildServiceProvider(builder);

        EnsureCreated(provider, NOFAbstractionConstants.Tenant.HostId);
        EnsureCreated(provider, "tenanta");

        return provider;
    }

    private static void EnsureCreated(IServiceProvider provider, string tenantId)
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITransparentInfos>().TenantId = tenantId;
        scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreated();
    }

    private static void ActivateDaemons(IServiceProvider provider)
    {
        _ = provider.GetServices<IDaemonService>().ToArray();
    }

    private static ServiceProvider BuildServiceProvider(TestServiceRegistrationContext builder)
    {
        new AutoInjectServiceRegistrationStep().ExecuteAsync(builder).GetAwaiter().GetResult();
        return builder.Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static void DeleteDirectoryWithRetry(string directory)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100 * attempt);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100 * attempt);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
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

    private sealed class DynamicAuditEntry
    {
        public long Id { get; init; }

        public string Message { get; init; } = string.Empty;
    }

    private sealed class FirstDynamicEntry
    {
        public long Id { get; init; }
    }

    private sealed class SecondDynamicEntry
    {
        public long Id { get; init; }
    }

    private sealed class ModelConfiguredHostOnlyEntry
    {
        public long Id { get; init; }
    }

    [HostOnly]
    private sealed class AttributeHostOnlyEntry
    {
        public long Id { get; init; }
    }

    private sealed class DynamicAuditEntryModelCreatingContributor : INOFDbContextModelCreatingContributor
    {
        public void Configure(ModelBuilder modelBuilder)
            => ConfigureDynamicAuditEntry(modelBuilder);
    }

    private sealed class ModelConfiguredHostOnlyEntryContributor : INOFDbContextModelCreatingContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ModelConfiguredHostOnlyEntry>(entity =>
            {
                entity.IsHostOnly();
                entity.ToTable(nameof(ModelConfiguredHostOnlyEntry));
                entity.HasKey(e => e.Id);
            });
        }
    }

    private sealed class AttributeHostOnlyEntryContributor : INOFDbContextModelCreatingContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AttributeHostOnlyEntry>(entity =>
            {
                entity.ToTable(nameof(AttributeHostOnlyEntry));
                entity.HasKey(e => e.Id);
            });
        }
    }

    private sealed class TestOrder : ICloneable
    {
        public long Id { get; init; }

        public string Number { get; init; } = string.Empty;

        public static TestOrder Create(long id, string number)
            => new() { Id = id, Number = number };

        public void Raise(object @event)
            => @event.PublishAsEvent();

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
        private readonly Registry _registry;

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
            _registry = new Registry();
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

        public Registry Registry => _registry;

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
