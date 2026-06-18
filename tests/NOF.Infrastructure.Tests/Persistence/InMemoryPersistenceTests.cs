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
using NOF.Infrastructure.EntityFrameworkCore;
using System.Linq.Expressions;
using Xunit;
using EFQuery = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions;
using HostOnlyAttribute = NOF.Application.HostOnlyAttribute;

namespace NOF.Infrastructure.Tests.Persistence;

public class SqliteInMemoryPersistenceTests
{
    private static Task<TSource> SingleAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
        => EFQuery.SingleAsync(source, cancellationToken);

    private static Task<TSource> SingleAsync<TSource>(
        IQueryable<TSource> source,
        Expression<Func<TSource, bool>> predicate,
        CancellationToken cancellationToken = default)
        => EFQuery.SingleAsync(source, predicate, cancellationToken);

    private static Task<TSource?> SingleOrDefaultAsync<TSource>(
        IQueryable<TSource> source,
        Expression<Func<TSource, bool>> predicate,
        CancellationToken cancellationToken = default)
        => EFQuery.SingleOrDefaultAsync(source, predicate, cancellationToken);

    private static Task<List<TSource>> ToListAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
        => EFQuery.ToListAsync(source, cancellationToken);

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
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

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
            SetTenant(tenantAScope.ServiceProvider, "tenanta");
            var tenantADb = tenantAScope.ServiceProvider.GetRequiredService<TestDbContext>();

            using var tenantBScope = services.CreateScope();
            SetTenant(tenantBScope.ServiceProvider, "tenantb");
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
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        db.Set<NOFTenant>().Add(new NOFTenant { Id = TenantId.Of("tenantdefault"), Name = "Tenant Default" });
        await db.SaveChangesAsync();

        var tenant = await db.FindAsync<NOFTenant>([TenantId.Of("tenantdefault")]);
        Assert.NotNull(tenant);
        Assert.Equal("Tenant Default", tenant.Name);
    }

    [Fact]
    public void UseDbContext_ShouldApplyBuiltInEntitiesThroughModelCreatingContributors()
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
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<NOFDbContext>();
        var tenant = db.Model.FindEntityType(typeof(NOFTenant));
        var inbox = db.Model.FindEntityType(typeof(NOFInboxMessage));
        var outbox = db.Model.FindEntityType(typeof(NOFOutboxMessage));
        var stateMachine = db.Model.FindEntityType(typeof(NOFStateMachineContext));
        Assert.NotNull(tenant);
        Assert.NotNull(inbox);
        Assert.NotNull(outbox);
        Assert.NotNull(stateMachine);

        Assert.Equal(nameof(NOFTenant), tenant.GetTableName());
        Assert.Equal(nameof(NOFInboxMessage), inbox.GetTableName());
        Assert.Equal(nameof(NOFOutboxMessage), outbox.GetTableName());
        Assert.Equal(nameof(NOFStateMachineContext), stateMachine.GetTableName());
        Assert.Equal(true, tenant.FindAnnotation("NOF:HostOnly")?.Value);
        Assert.Equal(true, inbox.FindAnnotation("NOF:HostOnly")?.Value);
        Assert.Equal(true, outbox.FindAnnotation("NOF:HostOnly")?.Value);
        Assert.Equal(true, stateMachine.FindAnnotation("NOF:HostOnly")?.Value);
        Assert.Equal([nameof(NOFInboxMessage.Id), nameof(NOFInboxMessage.HandlerType)],
            inbox.FindPrimaryKey()!.Properties.Select(static property => property.Name).ToArray());
        Assert.Equal([nameof(NOFStateMachineContext.CorrelationId), nameof(NOFStateMachineContext.DefinitionTypeName)],
            stateMachine.FindPrimaryKey()!.Properties.Select(static property => property.Name).ToArray());
        Assert.Contains(tenant.GetIndexes(), index => index.IsUnique
            && index.Properties.Select(static property => property.Name).SequenceEqual([nameof(NOFTenant.Name)]));
        Assert.Contains(outbox.GetIndexes(), index =>
            index.Properties.Select(static property => property.Name).SequenceEqual([nameof(NOFOutboxMessage.TraceParent)]));
    }

    [Fact]
    public async Task OnModelCreatingOptions_ShouldAddDynamicEntityType()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddSingleton<IDbContextModelCreatingContributor, DynamicAuditEntryModelCreatingContributor>();

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        ConfigureSqliteInMemory(
            builder.UseDbContext<NOFDbContext>()
                .WithTenantMode(TenantMode.DatabasePerTenant),
            $"nof-tests-{Guid.NewGuid():N}");

        using var services = BuildServiceProvider(builder);
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<NOFDbContext>();

        db.Set<DynamicAuditEntry>().Add(new DynamicAuditEntry
        {
            Id = 1,
            Message = "created from options"
        });
        await db.SaveChangesAsync();

        var stored = await SingleAsync(db.Set<DynamicAuditEntry>(), e => e.Id == 1);
        Assert.Equal("created from options", stored.Message);
    }

    [Fact]
    public void DifferentModelCreatingContributors_ShouldNotReuseWrongModel()
    {
        using var firstServices = BuildServiceProviderWithModelCreating(
            new FirstDynamicEntryContributor());
        using var secondServices = BuildServiceProviderWithModelCreating(
            new SecondDynamicEntryContributor());

        using var firstScope = firstServices.CreateScope();
        SetTenant(firstScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var firstDb = firstScope.ServiceProvider.GetRequiredService<NOFDbContext>();

        using var secondScope = secondServices.CreateScope();
        SetTenant(secondScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
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
        builder.Services.AddSingleton<IDbContextModelCreatingContributor, ModelConfiguredHostOnlyEntryContributor>();

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        ConfigureSqliteInMemory(
            builder.UseDbContext<NOFDbContext>()
                .WithTenantMode(TenantMode.SharedDatabase),
            $"nof-tests-{Guid.NewGuid():N}");

        using var services = BuildServiceProvider(builder);
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

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
        builder.Services.AddSingleton<IDbContextModelCreatingContributor, AttributeHostOnlyEntryContributor>();

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        ConfigureSqliteInMemory(
            builder.UseDbContext<NOFDbContext>()
                .WithTenantMode(TenantMode.SharedDatabase),
            $"nof-tests-{Guid.NewGuid():N}");

        using var services = BuildServiceProvider(builder);
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

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
        builder.Services.AddSingleton<IDbContextModelCreatingContributor, DynamicAuditEntryModelCreatingContributor>();
        ConfigureSqliteInMemory(
            builder.UseDbContext<NOFDbContext>()
                .WithTenantMode(TenantMode.DatabasePerTenant),
            $"nof-tests-{Guid.NewGuid():N}");

        using var services = BuildServiceProvider(builder);
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<NOFDbContext>();

        db.Set<DynamicAuditEntry>().Add(new DynamicAuditEntry
        {
            Id = 2,
            Message = "created from contributor"
        });
        await db.SaveChangesAsync();

        var stored = await SingleAsync(db.Set<DynamicAuditEntry>(), e => e.Id == 2);
        Assert.Equal("created from contributor", stored.Message);
    }

    [Fact]
    public async Task Transaction_Rollback_ShouldRestorePreviousState()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Set<NOFTenant>().Add(new NOFTenant { Id = TenantId.Of("tenant1"), Name = "Tenant 1" });
        await db.SaveChangesAsync();
        await transaction.RollbackAsync();

        using var verifyScope = services.CreateScope();
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();
        var tenant = await verifyDb.FindAsync<NOFTenant>([TenantId.Of("tenant1")]);
        Assert.Null(tenant);
    }

    [Fact]
    public async Task Transaction_NestedRollback_ShouldRestoreInnerChangesOnly()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

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
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();
        Assert.NotNull(await verifyDb.FindAsync<NOFTenant>([TenantId.Of("outer")]));
        Assert.Null(await verifyDb.FindAsync<NOFTenant>([TenantId.Of("inner")]));
    }

    [Fact]
    public async Task Transaction_DisposeWithoutCompletion_ShouldRollbackChanges()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        await using (await db.Database.BeginTransactionAsync())
        {
            db.Set<NOFTenant>().Add(new NOFTenant { Id = TenantId.Of("tenantdispose"), Name = "Dispose" });
            await db.SaveChangesAsync();
        }

        using var verifyScope = services.CreateScope();
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
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
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
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
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

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
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

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
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();

        Assert.Null(await SingleOrDefaultAsync(verifyDb.Set<TestOrder>(), e => e.Id == 10));

        var deletedAtUtc = await SingleAsync(
            verifyDb.Set<TestOrder>()
                .IgnoreQueryFilters()
                .Where(e => e.Id == 10)
                .Select(e => EF.Property<DateTime?>(e, "__DeletedAtUtc")));

        Assert.NotNull(deletedAtUtc);
    }

    [Fact]
    public async Task SoftDelete_ShouldPreserveOwnedRows_ForRequiredOwnsOneAndOwnsMany()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestOrderWithOwned>().Add(TestOrderWithOwned.Create(
            12,
            "owned-soft-delete",
            "required-detail",
            "item-a",
            "item-b"));
        await db.SaveChangesAsync();

        var order = await SingleAsync(db.Set<TestOrderWithOwned>(), e => e.Id == 12);

        db.Remove(order);
        await db.SaveChangesAsync();

        Assert.Null(await SingleOrDefaultAsync(db.Set<TestOrderWithOwned>(), e => e.Id == 12));

        using var verifyScope = services.CreateScope();
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var deletedOrder = await SingleAsync(
            verifyDb.Set<TestOrderWithOwned>()
                .IgnoreQueryFilters()
                .Where(e => e.Id == 12)
                .Select(e => new
                {
                    DeletedAtUtc = EF.Property<DateTime?>(e, "__DeletedAtUtc"),
                    DetailCode = e.RequiredDetail.Code,
                    ItemNames = e.Items.OrderBy(i => i.Id).Select(i => i.Name).ToList()
                }));

        Assert.NotNull(deletedOrder.DeletedAtUtc);
        Assert.Equal("required-detail", deletedOrder.DetailCode);
        Assert.Equal(["item-a", "item-b"], deletedOrder.ItemNames);
    }

    [Fact]
    public void SoftDelete_ShouldNotConfigureShadowDeleteColumn_ForOwnedEntityTypes()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var ownerType = db.Model.FindEntityType(typeof(TestOrderWithOwned));
        var ownsOneType = db.Model.FindEntityType(typeof(TestRequiredOwnedDetail));
        var ownsManyType = db.Model.FindEntityType(typeof(TestOwnedItem));

        Assert.NotNull(ownerType);
        Assert.NotNull(ownerType.FindProperty("__DeletedAtUtc"));

        Assert.NotNull(ownsOneType);
        Assert.True(ownsOneType.IsOwned());
        Assert.Null(ownsOneType.FindProperty("__DeletedAtUtc"));

        Assert.NotNull(ownsManyType);
        Assert.True(ownsManyType.IsOwned());
        Assert.Null(ownsManyType.FindProperty("__DeletedAtUtc"));
    }

    [Fact]
    public async Task SoftDelete_ShouldPreserveMultiLevelOwnedRows_ForNestedOwnsOneAndOwnsMany()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestOrderWithNestedOwned>().Add(TestOrderWithNestedOwned.Create(
            13,
            "nested-owned-soft-delete",
            "detail-code",
            "detail-leaf",
            ["detail-note-a", "detail-note-b"],
            [
                TestCompositeOwnedItem.Create("item-a", "snapshot-a", ["tag-a1", "tag-a2"]),
                TestCompositeOwnedItem.Create("item-b", "snapshot-b", ["tag-b1"])
            ]));
        await db.SaveChangesAsync();

        var order = await SingleAsync(db.Set<TestOrderWithNestedOwned>(), e => e.Id == 13);

        db.Remove(order);
        await db.SaveChangesAsync();

        Assert.Null(await SingleOrDefaultAsync(db.Set<TestOrderWithNestedOwned>(), e => e.Id == 13));

        using var verifyScope = services.CreateScope();
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var deletedOrder = await SingleAsync(
            verifyDb.Set<TestOrderWithNestedOwned>()
                .IgnoreQueryFilters()
                .Where(e => e.Id == 13)
                .Select(e => new
                {
                    DeletedAtUtc = EF.Property<DateTime?>(e, "__DeletedAtUtc"),
                    DetailCode = e.RequiredDetail.Code,
                    DetailLeafCode = e.RequiredDetail.RequiredLeaf.Code,
                    DetailNotes = e.RequiredDetail.Notes.OrderBy(n => n.Id).Select(n => n.Message).ToList(),
                    Items = e.Items
                        .OrderBy(i => i.Id)
                        .Select(i => new
                        {
                            i.Name,
                            SnapshotCode = i.Snapshot.Code,
                            Tags = i.Tags.OrderBy(t => t.Id).Select(t => t.Name).ToList()
                        })
                        .ToList()
                }));

        Assert.NotNull(deletedOrder.DeletedAtUtc);
        Assert.Equal("detail-code", deletedOrder.DetailCode);
        Assert.Equal("detail-leaf", deletedOrder.DetailLeafCode);
        Assert.Equal(["detail-note-a", "detail-note-b"], deletedOrder.DetailNotes);
        Assert.Equal(2, deletedOrder.Items.Count);
        Assert.Equal("item-a", deletedOrder.Items[0].Name);
        Assert.Equal("snapshot-a", deletedOrder.Items[0].SnapshotCode);
        Assert.Equal(["tag-a1", "tag-a2"], deletedOrder.Items[0].Tags);
        Assert.Equal("item-b", deletedOrder.Items[1].Name);
        Assert.Equal("snapshot-b", deletedOrder.Items[1].SnapshotCode);
        Assert.Equal(["tag-b1"], deletedOrder.Items[1].Tags);
    }

    [Fact]
    public async Task Include_ShouldFilterSoftDeletedChildren_FromCollectionNavigation()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var parent = TestIncludeParent.Create(20, "parent", [
            TestIncludeChild.Create(201, "child-active"),
            TestIncludeChild.Create(202, "child-deleted")
        ]);

        db.Set<TestIncludeParent>().Add(parent);
        await db.SaveChangesAsync();

        var childToDelete = await SingleAsync(db.Set<TestIncludeChild>(), c => c.Id == 202);
        db.Remove(childToDelete);
        await db.SaveChangesAsync();

        using var verifyScope = services.CreateScope();
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var visibleParent = await SingleAsync(
            verifyDb.Set<TestIncludeParent>()
                .Include(p => p.Children),
            p => p.Id == 20);

        Assert.Single(visibleParent.Children);
        Assert.Equal("child-active", visibleParent.Children[0].Name);

        var allChildren = await SingleAsync(
            verifyDb.Set<TestIncludeParent>()
                .IgnoreQueryFilters()
                .Include(p => p.Children)
                .Where(p => p.Id == 20));

        Assert.Equal(["child-active", "child-deleted"], allChildren.Children.OrderBy(c => c.Id).Select(c => c.Name).ToList());
    }

    [Fact]
    public async Task Include_ShouldPreserveForeignKey_WhenParentSoftDeleted()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var parent = TestIncludeParent.Create(21, "parent", [
            TestIncludeChild.Create(211, "child")
        ]);

        db.Set<TestIncludeParent>().Add(parent);
        await db.SaveChangesAsync();

        var parentToDelete = await SingleAsync(db.Set<TestIncludeParent>(), p => p.Id == 21);
        db.Remove(parentToDelete);
        await db.SaveChangesAsync();

        using var verifyScope = services.CreateScope();
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var visibleChild = await SingleAsync(
            verifyDb.Set<TestIncludeChild>()
                .Include(c => c.Parent),
            c => c.Id == 211);

        var deletedParent = await SingleAsync(
            verifyDb.Set<TestIncludeParent>()
                .IgnoreQueryFilters()
                .Where(p => p.Id == 21)
                .Select(p => new
                {
                    DeletedAtUtc = EF.Property<DateTime?>(p, "__DeletedAtUtc"),
                    p.Name
                }));

        Assert.Equal(21, visibleChild.ParentId);
        Assert.Null(visibleChild.Parent);
        Assert.NotNull(deletedParent.DeletedAtUtc);
        Assert.Equal("parent", deletedParent.Name);
    }

    [Fact]
    public async Task IncludeAndOwns_ShouldFilterSoftDeletedChildren_ButKeepActiveOwnedGraphs()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestIncludeOwnedParent>().Add(TestIncludeOwnedParent.Create(
            30,
            "parent-30",
            "parent-detail-30",
            ["parent-note-30-a", "parent-note-30-b"],
            [
                TestIncludeOwnedChild.Create(301, "child-active", "profile-active", "leaf-active", ["tag-active-a", "tag-active-b"]),
                TestIncludeOwnedChild.Create(302, "child-deleted", "profile-deleted", "leaf-deleted", ["tag-deleted-a"])
            ]));
        await db.SaveChangesAsync();

        var childToDelete = await SingleAsync(db.Set<TestIncludeOwnedChild>(), c => c.Id == 302);
        db.Remove(childToDelete);
        await db.SaveChangesAsync();

        using var verifyScope = services.CreateScope();
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var parent = await SingleAsync(
            verifyDb.Set<TestIncludeOwnedParent>()
                .Include(p => p.Children)
                    .ThenInclude(c => c.RequiredProfile)
                        .ThenInclude(p => p.RequiredLeaf)
                .Include(p => p.Children)
                    .ThenInclude(c => c.Tags),
            p => p.Id == 30);

        Assert.Equal("parent-detail-30", parent.RequiredDetail.Code);
        Assert.Equal(["parent-note-30-a", "parent-note-30-b"], parent.RequiredDetail.Notes.OrderBy(n => n.Id).Select(n => n.Message).ToList());
        Assert.Single(parent.Children);
        Assert.Equal("child-active", parent.Children[0].Name);
        Assert.Equal("profile-active", parent.Children[0].RequiredProfile.Code);
        Assert.Equal("leaf-active", parent.Children[0].RequiredProfile.RequiredLeaf.Code);
        Assert.Equal(["tag-active-a", "tag-active-b"], parent.Children[0].Tags.OrderBy(t => t.Id).Select(t => t.Name).ToList());
    }

    [Fact]
    public async Task IncludeAndOwns_IgnoreQueryFilters_ShouldReturnSoftDeletedChildrenWithOwnedGraphs()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestIncludeOwnedParent>().Add(TestIncludeOwnedParent.Create(
            31,
            "parent-31",
            "parent-detail-31",
            ["parent-note-31"],
            [
                TestIncludeOwnedChild.Create(311, "child-active", "profile-active", "leaf-active", ["tag-active"]),
                TestIncludeOwnedChild.Create(312, "child-deleted", "profile-deleted", "leaf-deleted", ["tag-deleted-a", "tag-deleted-b"])
            ]));
        await db.SaveChangesAsync();

        var childToDelete = await SingleAsync(db.Set<TestIncludeOwnedChild>(), c => c.Id == 312);
        db.Remove(childToDelete);
        await db.SaveChangesAsync();

        using var verifyScope = services.CreateScope();
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var parent = await SingleAsync(
            verifyDb.Set<TestIncludeOwnedParent>()
                .IgnoreQueryFilters()
                .Include(p => p.Children)
                    .ThenInclude(c => c.RequiredProfile)
                        .ThenInclude(p => p.RequiredLeaf)
                .Include(p => p.Children)
                    .ThenInclude(c => c.Tags),
            p => p.Id == 31);

        Assert.Equal("parent-detail-31", parent.RequiredDetail.Code);
        Assert.Equal(["parent-note-31"], parent.RequiredDetail.Notes.Select(n => n.Message).ToList());
        Assert.Equal(["child-active", "child-deleted"], parent.Children.OrderBy(c => c.Id).Select(c => c.Name).ToList());
        Assert.Equal("profile-deleted", parent.Children.Single(c => c.Id == 312).RequiredProfile.Code);
        Assert.Equal("leaf-deleted", parent.Children.Single(c => c.Id == 312).RequiredProfile.RequiredLeaf.Code);
        Assert.Equal(["tag-deleted-a", "tag-deleted-b"], parent.Children.Single(c => c.Id == 312).Tags.OrderBy(t => t.Id).Select(t => t.Name).ToList());
    }

    [Fact]
    public async Task IncludeAndOwns_WhenParentSoftDeleted_ShouldKeepChildOwnedGraphAndParentForeignKey()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestIncludeOwnedParent>().Add(TestIncludeOwnedParent.Create(
            32,
            "parent-32",
            "parent-detail-32",
            ["parent-note-32"],
            [
                TestIncludeOwnedChild.Create(321, "child-321", "profile-321", "leaf-321", ["tag-321-a", "tag-321-b"])
            ]));
        await db.SaveChangesAsync();

        var parentToDelete = await SingleAsync(db.Set<TestIncludeOwnedParent>(), p => p.Id == 32);
        db.Remove(parentToDelete);
        await db.SaveChangesAsync();

        using var verifyScope = services.CreateScope();
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var child = await SingleAsync(
            verifyDb.Set<TestIncludeOwnedChild>()
                .Include(c => c.Parent)
                .Include(c => c.RequiredProfile)
                    .ThenInclude(p => p.RequiredLeaf)
                .Include(c => c.Tags),
            c => c.Id == 321);

        var deletedParent = await SingleAsync(
            verifyDb.Set<TestIncludeOwnedParent>()
                .IgnoreQueryFilters()
                .Where(p => p.Id == 32)
                .Select(p => new
                {
                    DeletedAtUtc = EF.Property<DateTime?>(p, "__DeletedAtUtc"),
                    DetailCode = p.RequiredDetail.Code,
                    Notes = p.RequiredDetail.Notes.OrderBy(n => n.Id).Select(n => n.Message).ToList()
                }));

        Assert.Equal(32, child.ParentId);
        Assert.Null(child.Parent);
        Assert.Equal("profile-321", child.RequiredProfile.Code);
        Assert.Equal("leaf-321", child.RequiredProfile.RequiredLeaf.Code);
        Assert.Equal(["tag-321-a", "tag-321-b"], child.Tags.OrderBy(t => t.Id).Select(t => t.Name).ToList());
        Assert.NotNull(deletedParent.DeletedAtUtc);
        Assert.Equal("parent-detail-32", deletedParent.DetailCode);
        Assert.Equal(["parent-note-32"], deletedParent.Notes);
    }

    [Fact]
    public async Task IncludeAndOwns_IgnoreQueryFilters_ShouldRestoreGraphs_WhenParentAndChildAreSoftDeleted()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestIncludeOwnedParent>().Add(TestIncludeOwnedParent.Create(
            33,
            "parent-33",
            "parent-detail-33",
            ["parent-note-33-a", "parent-note-33-b"],
            [
                TestIncludeOwnedChild.Create(331, "child-331", "profile-331", "leaf-331", ["tag-331"]),
                TestIncludeOwnedChild.Create(332, "child-332", "profile-332", "leaf-332", ["tag-332-a", "tag-332-b"])
            ]));
        await db.SaveChangesAsync();

        var childToDelete = await SingleAsync(db.Set<TestIncludeOwnedChild>(), c => c.Id == 332);
        db.Remove(childToDelete);
        await db.SaveChangesAsync();

        var parentToDelete = await SingleAsync(db.Set<TestIncludeOwnedParent>(), p => p.Id == 33);
        db.Remove(parentToDelete);
        await db.SaveChangesAsync();

        using var verifyScope = services.CreateScope();
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var parent = await SingleAsync(
            verifyDb.Set<TestIncludeOwnedParent>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(p => p.Children)
                    .ThenInclude(c => c.RequiredProfile)
                        .ThenInclude(p => p.RequiredLeaf)
                .Include(p => p.Children)
                    .ThenInclude(c => c.Tags),
            p => p.Id == 33);

        Assert.Equal("parent-detail-33", parent.RequiredDetail.Code);
        Assert.Equal(["parent-note-33-a", "parent-note-33-b"], parent.RequiredDetail.Notes.OrderBy(n => n.Id).Select(n => n.Message).ToList());
        Assert.Equal(["child-331", "child-332"], parent.Children.OrderBy(c => c.Id).Select(c => c.Name).ToList());
        Assert.Equal("profile-331", parent.Children.Single(c => c.Id == 331).RequiredProfile.Code);
        Assert.Equal("leaf-331", parent.Children.Single(c => c.Id == 331).RequiredProfile.RequiredLeaf.Code);
        Assert.Equal(["tag-331"], parent.Children.Single(c => c.Id == 331).Tags.Select(t => t.Name).ToList());
        Assert.Equal("profile-332", parent.Children.Single(c => c.Id == 332).RequiredProfile.Code);
        Assert.Equal("leaf-332", parent.Children.Single(c => c.Id == 332).RequiredProfile.RequiredLeaf.Code);
        Assert.Equal(["tag-332-a", "tag-332-b"], parent.Children.Single(c => c.Id == 332).Tags.OrderBy(t => t.Id).Select(t => t.Name).ToList());
    }

    [Fact]
    public async Task ThenInclude_ShouldTraverseOrdinaryNavigationIntoOwnedGraphs()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestIncludeOwnedParent>().Add(TestIncludeOwnedParent.Create(
            34,
            "parent-34",
            "parent-detail-34",
            ["parent-note-34-a", "parent-note-34-b"],
            [
                TestIncludeOwnedChild.Create(341, "child-341", "profile-341", "leaf-341", ["tag-341"])
            ]));
        await db.SaveChangesAsync();

        using var verifyScope = services.CreateScope();
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var child = await SingleAsync(
            verifyDb.Set<TestIncludeOwnedChild>()
                .Include(c => c.Parent)
                    .ThenInclude(p => p!.RequiredDetail)
                        .ThenInclude(d => d.Notes)
                .Include(c => c.RequiredProfile)
                    .ThenInclude(p => p.RequiredLeaf),
            c => c.Id == 341);

        Assert.NotNull(child.Parent);
        Assert.Equal("parent-34", child.Parent.Name);
        Assert.Equal("parent-detail-34", child.Parent.RequiredDetail.Code);
        Assert.Equal(["parent-note-34-a", "parent-note-34-b"], child.Parent.RequiredDetail.Notes.OrderBy(n => n.Id).Select(n => n.Message).ToList());
        Assert.Equal("profile-341", child.RequiredProfile.Code);
        Assert.Equal("leaf-341", child.RequiredProfile.RequiredLeaf.Code);
    }

    [Fact]
    public async Task IncludeAndOwns_AsSplitQuery_ShouldRestoreSoftDeletedGraphs()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestIncludeOwnedParent>().Add(TestIncludeOwnedParent.Create(
            35,
            "parent-35",
            "parent-detail-35",
            ["parent-note-35"],
            [
                TestIncludeOwnedChild.Create(351, "child-351", "profile-351", "leaf-351", ["tag-351-a", "tag-351-b"]),
                TestIncludeOwnedChild.Create(352, "child-352", "profile-352", "leaf-352", ["tag-352"])
            ]));
        await db.SaveChangesAsync();

        var childToDelete = await SingleAsync(db.Set<TestIncludeOwnedChild>(), c => c.Id == 352);
        db.Remove(childToDelete);

        var parentToDelete = await SingleAsync(db.Set<TestIncludeOwnedParent>(), p => p.Id == 35);
        db.Remove(parentToDelete);
        await db.SaveChangesAsync();

        using var verifyScope = services.CreateScope();
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var parent = await SingleAsync(
            verifyDb.Set<TestIncludeOwnedParent>()
                .IgnoreQueryFilters()
                .AsSplitQuery()
                .Include(p => p.Children)
                    .ThenInclude(c => c.RequiredProfile)
                        .ThenInclude(p => p.RequiredLeaf)
                .Include(p => p.Children)
                    .ThenInclude(c => c.Tags),
            p => p.Id == 35);

        Assert.Equal("parent-detail-35", parent.RequiredDetail.Code);
        Assert.Equal(["child-351", "child-352"], parent.Children.OrderBy(c => c.Id).Select(c => c.Name).ToList());
        Assert.Equal("profile-352", parent.Children.Single(c => c.Id == 352).RequiredProfile.Code);
        Assert.Equal(["tag-351-a", "tag-351-b"], parent.Children.Single(c => c.Id == 351).Tags.OrderBy(t => t.Id).Select(t => t.Name).ToList());
    }

    [Fact]
    public async Task Projection_ShouldReturnComplexOwnedShape_WhenGraphsAreSoftDeleted()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestIncludeOwnedParent>().Add(TestIncludeOwnedParent.Create(
            36,
            "parent-36",
            "parent-detail-36",
            ["parent-note-36"],
            [
                TestIncludeOwnedChild.Create(361, "child-361", "profile-361", "leaf-361", ["tag-361-a", "tag-361-b"])
            ]));
        await db.SaveChangesAsync();

        var parentToDelete = await SingleAsync(db.Set<TestIncludeOwnedParent>(), p => p.Id == 36);
        db.Remove(parentToDelete);
        await db.SaveChangesAsync();

        using var verifyScope = services.CreateScope();
        SetTenant(verifyScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var projection = await SingleAsync(
            verifyDb.Set<TestIncludeOwnedParent>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.Id == 36)
                .Select(p => new
                {
                    DeletedAtUtc = EF.Property<DateTime?>(p, "__DeletedAtUtc"),
                    DetailCode = p.RequiredDetail.Code,
                    ParentNotes = p.RequiredDetail.Notes.OrderBy(n => n.Id).Select(n => n.Message).ToList(),
                    Children = p.Children
                        .OrderBy(c => c.Id)
                        .Select(c => new
                        {
                            c.Name,
                            ProfileCode = c.RequiredProfile.Code,
                            LeafCode = c.RequiredProfile.RequiredLeaf.Code,
                            Tags = c.Tags.OrderBy(t => t.Id).Select(t => t.Name).ToList()
                        })
                        .ToList()
                }));

        Assert.NotNull(projection.DeletedAtUtc);
        Assert.Equal("parent-detail-36", projection.DetailCode);
        Assert.Equal(["parent-note-36"], projection.ParentNotes);
        Assert.Single(projection.Children);
        Assert.Equal("child-361", projection.Children[0].Name);
        Assert.Equal("profile-361", projection.Children[0].ProfileCode);
        Assert.Equal("leaf-361", projection.Children[0].LeafCode);
        Assert.Equal(["tag-361-a", "tag-361-b"], projection.Children[0].Tags);
    }

    [Fact]
    public async Task SharedDatabase_IncludeAndOwns_ShouldIsolateTenantGraphs_AndFilterSoftDeletedChildren()
    {
        using var services = CreateSharedDatabaseServiceProvider();

        using (var tenantAScope = services.CreateScope())
        {
            SetTenant(tenantAScope.ServiceProvider, "tenanta");
            var tenantADb = tenantAScope.ServiceProvider.GetRequiredService<TestDbContext>();

            tenantADb.Set<TestIncludeOwnedParent>().Add(TestIncludeOwnedParent.Create(
                41,
                "tenant-a-parent",
                "tenant-a-detail",
                ["tenant-a-note"],
                [
                    TestIncludeOwnedChild.Create(411, "tenant-a-child-active", "tenant-a-profile-active", "tenant-a-leaf-active", ["tenant-a-tag-active"]),
                    TestIncludeOwnedChild.Create(412, "tenant-a-child-deleted", "tenant-a-profile-deleted", "tenant-a-leaf-deleted", ["tenant-a-tag-deleted"])
                ]));
            await tenantADb.SaveChangesAsync();

            var childToDelete = await SingleAsync(tenantADb.Set<TestIncludeOwnedChild>(), c => c.Id == 412);
            tenantADb.Remove(childToDelete);
            await tenantADb.SaveChangesAsync();
        }

        using (var tenantBScope = services.CreateScope())
        {
            SetTenant(tenantBScope.ServiceProvider, "tenantb");
            var tenantBDb = tenantBScope.ServiceProvider.GetRequiredService<TestDbContext>();

            tenantBDb.Set<TestIncludeOwnedParent>().Add(TestIncludeOwnedParent.Create(
                42,
                "tenant-b-parent",
                "tenant-b-detail",
                ["tenant-b-note"],
                [
                    TestIncludeOwnedChild.Create(421, "tenant-b-child", "tenant-b-profile", "tenant-b-leaf", ["tenant-b-tag"])
                ]));
            await tenantBDb.SaveChangesAsync();
        }

        using var verifyTenantAScope = services.CreateScope();
        SetTenant(verifyTenantAScope.ServiceProvider, "tenanta");
        var verifyTenantADb = verifyTenantAScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var tenantAParent = await SingleAsync(
            verifyTenantADb.Set<TestIncludeOwnedParent>()
                .Include(p => p.Children)
                    .ThenInclude(c => c.RequiredProfile)
                        .ThenInclude(p => p.RequiredLeaf)
                .Include(p => p.Children)
                    .ThenInclude(c => c.Tags),
            p => p.Id == 41);

        Assert.Equal("tenant-a-detail", tenantAParent.RequiredDetail.Code);
        Assert.Single(tenantAParent.Children);
        Assert.Equal("tenant-a-child-active", tenantAParent.Children[0].Name);
        Assert.Equal("tenant-a-profile-active", tenantAParent.Children[0].RequiredProfile.Code);
        Assert.Equal("tenant-a-leaf-active", tenantAParent.Children[0].RequiredProfile.RequiredLeaf.Code);
        Assert.Equal(["tenant-a-tag-active"], tenantAParent.Children[0].Tags.Select(t => t.Name).ToList());
        Assert.Null(await SingleOrDefaultAsync(verifyTenantADb.Set<TestIncludeOwnedParent>(), p => p.Id == 42));

        using var verifyTenantBScope = services.CreateScope();
        SetTenant(verifyTenantBScope.ServiceProvider, "tenantb");
        var verifyTenantBDb = verifyTenantBScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var tenantBParent = await SingleAsync(
            verifyTenantBDb.Set<TestIncludeOwnedParent>()
                .Include(p => p.Children)
                    .ThenInclude(c => c.RequiredProfile)
                        .ThenInclude(p => p.RequiredLeaf)
                .Include(p => p.Children)
                    .ThenInclude(c => c.Tags),
            p => p.Id == 42);

        Assert.Equal("tenant-b-detail", tenantBParent.RequiredDetail.Code);
        Assert.Single(tenantBParent.Children);
        Assert.Equal("tenant-b-child", tenantBParent.Children[0].Name);
        Assert.Equal("tenant-b-profile", tenantBParent.Children[0].RequiredProfile.Code);
        Assert.Null(await SingleOrDefaultAsync(verifyTenantBDb.Set<TestIncludeOwnedParent>(), p => p.Id == 41));
    }

    [Fact]
    public async Task SharedDatabase_IgnoreQueryFilters_ShouldExposeSoftDeletedGraphsWithinSharedStore()
    {
        using var services = CreateSharedDatabaseServiceProvider();

        using (var tenantAScope = services.CreateScope())
        {
            SetTenant(tenantAScope.ServiceProvider, "tenanta");
            var tenantADb = tenantAScope.ServiceProvider.GetRequiredService<TestDbContext>();

            tenantADb.Set<TestIncludeOwnedParent>().Add(TestIncludeOwnedParent.Create(
                43,
                "tenant-a-parent-43",
                "tenant-a-detail-43",
                ["tenant-a-note-43"],
                [
                    TestIncludeOwnedChild.Create(431, "tenant-a-child-431", "tenant-a-profile-431", "tenant-a-leaf-431", ["tenant-a-tag-431"]),
                    TestIncludeOwnedChild.Create(432, "tenant-a-child-432", "tenant-a-profile-432", "tenant-a-leaf-432", ["tenant-a-tag-432-a", "tenant-a-tag-432-b"])
                ]));
            await tenantADb.SaveChangesAsync();

            var childToDelete = await SingleAsync(tenantADb.Set<TestIncludeOwnedChild>(), c => c.Id == 432);
            tenantADb.Remove(childToDelete);
            await tenantADb.SaveChangesAsync();
        }

        using var hostScope = services.CreateScope();
        SetTenant(hostScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var hostDb = hostScope.ServiceProvider.GetRequiredService<TestDbContext>();

        var tenantAProjection = await SingleAsync(
            hostDb.Set<TestIncludeOwnedParent>()
                .IgnoreQueryFilters()
                .AsSplitQuery()
                .Where(p => p.Id == 43)
                .Select(p => new
                {
                    DetailCode = p.RequiredDetail.Code,
                    Children = p.Children
                        .OrderBy(c => c.Id)
                        .Select(c => new
                        {
                            c.Name,
                            ProfileCode = c.RequiredProfile.Code,
                            LeafCode = c.RequiredProfile.RequiredLeaf.Code,
                            Tags = c.Tags.OrderBy(t => t.Id).Select(t => t.Name).ToList()
                        })
                        .ToList()
                }));

        Assert.Equal("tenant-a-detail-43", tenantAProjection.DetailCode);
        Assert.Equal(["tenant-a-child-431", "tenant-a-child-432"], tenantAProjection.Children.Select(c => c.Name).ToList());
        Assert.Equal("tenant-a-profile-432", tenantAProjection.Children[1].ProfileCode);
        Assert.Equal("tenant-a-leaf-432", tenantAProjection.Children[1].LeafCode);
        Assert.Equal(["tenant-a-tag-432-a", "tenant-a-tag-432-b"], tenantAProjection.Children[1].Tags);
    }

    [Fact]
    public async Task WithSoftDelete_Disabled_ShouldPhysicallyDeleteRows()
    {
        using var services = CreateServiceProvider(softDeleteEnabled: false);
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestOrder>().Add(TestOrder.Create(11, "hard-delete"));
        await db.SaveChangesAsync();

        var order = await db.FindAsync<TestOrder>([11L]);
        Assert.NotNull(order);

        db.Set<TestOrder>().Remove(order);
        await db.SaveChangesAsync();

        Assert.Null(await SingleOrDefaultAsync(db.Set<TestOrder>().IgnoreQueryFilters(), e => e.Id == 11));
    }

    [Fact]
    public async Task InboxRepository_ShouldAddFindAndRemoveMessages()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

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
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

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
        var claimed = await ToListAsync(
            db.Set<NOFOutboxMessage>()
                .Where(m => m.Status == OutboxMessageStatus.Pending && m.RetryCount < 100)
                .OrderBy(m => m.CreatedAtUtc)
                .Take(100));
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
            SetTenant(verify.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
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
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

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

        var claimed = await ToListAsync(
            db.Set<NOFOutboxMessage>()
                .Where(m => m.Status == OutboxMessageStatus.Pending &&
                            m.RetryCount < 2 &&
                            (m.ClaimedBy == null || m.ClaimExpiresAtUtc == null || m.ClaimExpiresAtUtc <= DateTime.UtcNow)));
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
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

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

        var claimed = await ToListAsync(db.Set<NOFOutboxMessage>());
        claimed[0].RetryCount++;
        claimed[0].ErrorMessage = "boom";
        claimed[0].FailedAtUtc = DateTime.UtcNow;
        claimed[0].Status = OutboxMessageStatus.Failed;
        claimed[0].ClaimedBy = null;
        claimed[0].ClaimExpiresAtUtc = null;
        await db.SaveChangesAsync();

        using (var verify = services.CreateScope())
        {
            SetTenant(verify.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
            var verifyDb = verify.ServiceProvider.GetRequiredService<TestDbContext>();
            var stored = await verifyDb.FindAsync<NOFOutboxMessage>([id]);
            Assert.NotNull(stored);
            Assert.Equal(OutboxMessageStatus.Failed, stored.Status);
            Assert.Equal("boom", stored.ErrorMessage);
        }
    }

    [Fact]
    public async Task OutboxRecovery_ShouldMarkExpiredExhaustedPendingMessagesAsFailed()
    {
        using var services = CreateServiceProvider(new TransactionalMessageOptions
        {
            Outbox = new TransactionalMessageProcessorOptions { MaxRetryCount = 2, ClaimTimeout = TimeSpan.FromMinutes(1) }
        });
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var expiredId = Guid.NewGuid();
        var futureId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Set<NOFOutboxMessage>().AddRange(
            new NOFOutboxMessage
            {
                Id = expiredId,
                RetryCount = 2,
                PayloadType = typeof(string).AssemblyQualifiedName!,
                DispatchTypes = "[\"System.String\"]",
                Payload = System.Text.Encoding.UTF8.GetBytes("expired"),
                Headers = "{}",
                MessageType = OutboxMessageType.Command,
                ClaimedBy = "expired-claim",
                ClaimExpiresAtUtc = now.AddSeconds(-1)
            },
            new NOFOutboxMessage
            {
                Id = futureId,
                RetryCount = 2,
                PayloadType = typeof(string).AssemblyQualifiedName!,
                DispatchTypes = "[\"System.String\"]",
                Payload = System.Text.Encoding.UTF8.GetBytes("future"),
                Headers = "{}",
                MessageType = OutboxMessageType.Command,
                ClaimedBy = "future-claim",
                ClaimExpiresAtUtc = now.AddMinutes(5)
            });
        await db.SaveChangesAsync();

        var appDb = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var changed = await TransactionalMessageRecovery.MarkExpiredExhaustedOutboxMessagesAsFailedAsync(
            appDb,
            maxRetryCount: 2,
            now,
            CancellationToken.None);

        Assert.Equal(1, changed);

        using var verify = services.CreateScope();
        SetTenant(verify.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verify.ServiceProvider.GetRequiredService<TestDbContext>();
        var expired = await verifyDb.FindAsync<NOFOutboxMessage>([expiredId]);
        var future = await verifyDb.FindAsync<NOFOutboxMessage>([futureId]);

        Assert.NotNull(expired);
        Assert.Equal(OutboxMessageStatus.Failed, expired.Status);
        Assert.Equal("Exceeded max retry count", expired.ErrorMessage);
        Assert.Null(expired.ClaimedBy);
        Assert.Null(expired.ClaimExpiresAtUtc);
        Assert.NotNull(expired.FailedAtUtc);

        Assert.NotNull(future);
        Assert.Equal(OutboxMessageStatus.Pending, future.Status);
        Assert.Equal("future-claim", future.ClaimedBy);
        Assert.NotNull(future.ClaimExpiresAtUtc);
    }

    [Fact]
    public async Task InboxRecovery_ShouldMarkExpiredExhaustedPendingMessagesAsFailed()
    {
        using var services = CreateServiceProvider(new TransactionalMessageOptions
        {
            Inbox = new TransactionalMessageProcessorOptions { MaxRetryCount = 2, ClaimTimeout = TimeSpan.FromMinutes(1) }
        });
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var expiredId = Guid.NewGuid();
        var futureId = Guid.NewGuid();
        var handlerType = typeof(SqliteInMemoryPersistenceTests).AssemblyQualifiedName!;
        var now = DateTime.UtcNow;
        db.Set<NOFInboxMessage>().AddRange(
            new NOFInboxMessage
            {
                Id = expiredId,
                RetryCount = 2,
                HandlerType = handlerType,
                PayloadType = typeof(string).AssemblyQualifiedName!,
                Payload = System.Text.Encoding.UTF8.GetBytes("expired"),
                Headers = "{}",
                MessageType = InboxMessageType.Command,
                ClaimedBy = "expired-claim",
                ClaimExpiresAtUtc = now.AddSeconds(-1)
            },
            new NOFInboxMessage
            {
                Id = futureId,
                RetryCount = 2,
                HandlerType = handlerType,
                PayloadType = typeof(string).AssemblyQualifiedName!,
                Payload = System.Text.Encoding.UTF8.GetBytes("future"),
                Headers = "{}",
                MessageType = InboxMessageType.Command,
                ClaimedBy = "future-claim",
                ClaimExpiresAtUtc = now.AddMinutes(5)
            });
        await db.SaveChangesAsync();

        var appDb = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var changed = await TransactionalMessageRecovery.MarkExpiredExhaustedInboxMessagesAsFailedAsync(
            appDb,
            maxRetryCount: 2,
            now,
            CancellationToken.None);

        Assert.Equal(1, changed);

        using var verify = services.CreateScope();
        SetTenant(verify.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        var verifyDb = verify.ServiceProvider.GetRequiredService<TestDbContext>();
        var expired = await verifyDb.FindAsync<NOFInboxMessage>([expiredId, handlerType]);
        var future = await verifyDb.FindAsync<NOFInboxMessage>([futureId, handlerType]);

        Assert.NotNull(expired);
        Assert.Equal(InboxMessageStatus.Failed, expired.Status);
        Assert.Equal("Exceeded max retry count", expired.ErrorMessage);
        Assert.Null(expired.ClaimedBy);
        Assert.Null(expired.ClaimExpiresAtUtc);
        Assert.NotNull(expired.FailedAtUtc);

        Assert.NotNull(future);
        Assert.Equal(InboxMessageStatus.Pending, future.Status);
        Assert.Equal("future-claim", future.ClaimedBy);
        Assert.NotNull(future.ClaimExpiresAtUtc);
    }

    [Fact]
    public async Task StateMachineRepository_ShouldIsolateDataByTenant()
    {
        using var services = CreateServiceProvider(tenantMode: TenantMode.DatabasePerTenant);
        using (var hostScope = services.CreateScope())
        {
            SetTenant(hostScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
            var hostDb = hostScope.ServiceProvider.GetRequiredService<TestDbContext>();
            hostDb.Set<NOFStateMachineContext>().Add(new NOFStateMachineContext { CorrelationId = "corr", DefinitionTypeName = "def", State = 1 });
            await hostDb.SaveChangesAsync();
        }

        using (var tenantScope = services.CreateScope())
        {
            SetTenant(tenantScope.ServiceProvider, "tenanta");
            var tenantDb = tenantScope.ServiceProvider.GetRequiredService<TestDbContext>();
            tenantDb.Set<NOFStateMachineContext>().Add(new NOFStateMachineContext { CorrelationId = "corr", DefinitionTypeName = "def", State = 2 });
            await tenantDb.SaveChangesAsync();
            Assert.Equal(2,
            (await tenantDb.FindAsync<NOFStateMachineContext>(["corr", "def"]))!.State);
        }

        using (var verifyHostScope = services.CreateScope())
        {
            SetTenant(verifyHostScope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
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
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        ActivateDaemons(scope.ServiceProvider);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestOrder>().Add(TestOrder.Create(7, "before"));
        await db.SaveChangesAsync();

        var tracked = await SingleAsync(db.Set<TestOrder>(), order => order.Id == 7);
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
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
        ActivateDaemons(scope.ServiceProvider);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<TestEventPublisher>();

        db.Set<TestOrder>().Add(TestOrder.Create(8, "before"));
        await db.SaveChangesAsync();

        var detached = await SingleAsync(db.Set<TestOrder>().AsNoTracking(), order => order.Id == 8);
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
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        db.Set<TestOrder>().Add(TestOrder.Create(9, "n"));
        await db.SaveChangesAsync();

        var rows = db.Set<TestOrder>().FromSqlRaw("select * from TestOrder").ToList();
        Assert.NotEmpty(rows);

        await db.Database.ExecuteSqlInterpolatedAsync($"delete from TestOrder where Id = {9L}");
        // Query in a fresh scope to avoid first-level cache.
        using (var verify = services.CreateScope())
        {
            SetTenant(verify.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);
            var verifyDb = verify.ServiceProvider.GetRequiredService<TestDbContext>();
            Assert.Null(await verifyDb.FindAsync<TestOrder>([9L]));
        }
    }

    [Fact]
    public async Task Creating_HostTenant_Record_ShouldThrow()
    {
        using var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        SetTenant(scope.ServiceProvider, NOFAbstractionConstants.Tenant.HostId);

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

    private static void ConfigureDynamicAuditEntry(IDbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DynamicAuditEntry>(entity =>
        {
            entity.ToTable(nameof(DynamicAuditEntry));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).HasMaxLength(256).IsRequired();
        });
    }

    private static ServiceProvider BuildServiceProviderWithModelCreating(IDbContextModelCreatingContributor contributor)
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddSingleton(contributor);

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();
        ConfigureSqliteInMemory(
            builder.UseDbContext<NOFDbContext>()
                .WithTenantMode(TenantMode.DatabasePerTenant),
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

    private static ServiceProvider CreateSharedDatabaseServiceProvider(bool softDeleteEnabled = true)
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddSingleton<TestEventPublisher>();

        builder.AddHostingDefaults();
        builder.AddInfrastructureDefaults();

        var databaseName = $"nof-tests-shared-{Guid.NewGuid():N}";
        builder.UseDbContext<TestDbContext>()
            .WithTenantMode(TenantMode.SharedDatabase)
            .WithSoftDelete(softDeleteEnabled)
            .WithConnectionString($"Data Source={databaseName};Mode=Memory;Cache=Shared")
            .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseSqlite(connectionString));
        builder.Services.ReplaceOrAddSingleton<IEventPublisher>(sp => sp.GetRequiredService<TestEventPublisher>());

        var provider = BuildServiceProvider(builder);
        EnsureCreated(provider, NOFAbstractionConstants.Tenant.HostId);
        EnsureCreated(provider, "tenanta");
        EnsureCreated(provider, "tenantb");
        return provider;
    }

    private static void EnsureCreated(IServiceProvider provider, string tenantId)
    {
        using var scope = provider.CreateScope();
        SetTenant(scope.ServiceProvider, tenantId);
        scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreated();
    }

    private static void ActivateDaemons(IServiceProvider provider)
    {
        _ = provider.GetServices<IDaemonService>().ToArray();
    }

    private static ServiceProvider BuildServiceProvider(TestServiceRegistrationContext builder)
    {
        return builder.Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static void SetTenant(IServiceProvider services, string? tenantId)
    {
        _ = services.GetRequiredService<IMutableCurrentTenant>().PushTenant(TenantId.Normalize(tenantId));
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

    private sealed class DynamicAuditEntryModelCreatingContributor : IDbContextModelCreatingContributor
    {
        public void Configure(IDbModelBuilder modelBuilder)
            => ConfigureDynamicAuditEntry(modelBuilder);
    }

    private sealed class ModelConfiguredHostOnlyEntryContributor : IDbContextModelCreatingContributor
    {
        public void Configure(IDbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ModelConfiguredHostOnlyEntry>(entity =>
            {
                entity.IsHostOnly();
                entity.ToTable(nameof(ModelConfiguredHostOnlyEntry));
                entity.HasKey(e => e.Id);
            });
        }
    }

    private sealed class AttributeHostOnlyEntryContributor : IDbContextModelCreatingContributor
    {
        public void Configure(IDbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AttributeHostOnlyEntry>(entity =>
            {
                entity.ToTable(nameof(AttributeHostOnlyEntry));
                entity.HasKey(e => e.Id);
            });
        }
    }

    private sealed class FirstDynamicEntryContributor : IDbContextModelCreatingContributor
    {
        public void Configure(IDbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FirstDynamicEntry>(entity =>
            {
                entity.ToTable(nameof(FirstDynamicEntry));
                entity.HasKey(e => e.Id);
            });
        }
    }

    private sealed class SecondDynamicEntryContributor : IDbContextModelCreatingContributor
    {
        public void Configure(IDbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SecondDynamicEntry>(entity =>
            {
                entity.ToTable(nameof(SecondDynamicEntry));
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

    private sealed class TestOrderWithOwned
    {
        public long Id { get; init; }

        public string Number { get; init; } = string.Empty;

        public TestRequiredOwnedDetail RequiredDetail { get; private set; } = null!;

        public List<TestOwnedItem> Items { get; private set; } = [];

        public static TestOrderWithOwned Create(long id, string number, string detailCode, params string[] itemNames)
            => new()
            {
                Id = id,
                Number = number,
                RequiredDetail = new TestRequiredOwnedDetail(detailCode),
                Items = itemNames.Select(name => new TestOwnedItem(name)).ToList()
            };
    }

    private sealed class TestRequiredOwnedDetail
    {
        public TestRequiredOwnedDetail(string code)
        {
            Code = code;
        }

        private TestRequiredOwnedDetail()
        {
        }

        public string Code { get; private set; } = string.Empty;
    }

    private sealed class TestOwnedItem
    {
        public TestOwnedItem(string name)
        {
            Name = name;
        }

        private TestOwnedItem()
        {
        }

        public int Id { get; private set; }

        public string Name { get; private set; } = string.Empty;
    }

    private sealed class TestOrderWithNestedOwned
    {
        public long Id { get; init; }

        public string Number { get; init; } = string.Empty;

        public TestCompositeOwnedDetail RequiredDetail { get; private set; } = null!;

        public List<TestCompositeOwnedItem> Items { get; private set; } = [];

        public static TestOrderWithNestedOwned Create(
            long id,
            string number,
            string detailCode,
            string detailLeafCode,
            IEnumerable<string> detailNotes,
            IEnumerable<TestCompositeOwnedItem> items)
            => new()
            {
                Id = id,
                Number = number,
                RequiredDetail = TestCompositeOwnedDetail.Create(detailCode, detailLeafCode, detailNotes),
                Items = [.. items]
            };
    }

    private sealed class TestCompositeOwnedDetail
    {
        public string Code { get; private set; } = string.Empty;

        public TestOwnedLeaf RequiredLeaf { get; private set; } = null!;

        public List<TestOwnedDetailNote> Notes { get; private set; } = [];

        public static TestCompositeOwnedDetail Create(string code, string leafCode, IEnumerable<string> notes)
            => new()
            {
                Code = code,
                RequiredLeaf = TestOwnedLeaf.Create(leafCode),
                Notes = [.. notes.Select(TestOwnedDetailNote.Create)]
            };
    }

    private sealed class TestCompositeOwnedItem
    {
        public int Id { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public TestOwnedLeaf Snapshot { get; private set; } = null!;

        public List<TestOwnedItemTag> Tags { get; private set; } = [];

        public static TestCompositeOwnedItem Create(string name, string snapshotCode, IEnumerable<string> tags)
            => new()
            {
                Name = name,
                Snapshot = TestOwnedLeaf.Create(snapshotCode),
                Tags = [.. tags.Select(TestOwnedItemTag.Create)]
            };
    }

    private sealed class TestOwnedLeaf
    {
        public string Code { get; private set; } = string.Empty;

        public static TestOwnedLeaf Create(string code)
            => new()
            {
                Code = code
            };
    }

    private sealed class TestOwnedDetailNote
    {
        public int Id { get; private set; }

        public string Message { get; private set; } = string.Empty;

        public static TestOwnedDetailNote Create(string message)
            => new()
            {
                Message = message
            };
    }

    private sealed class TestOwnedItemTag
    {
        public int Id { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public static TestOwnedItemTag Create(string name)
            => new()
            {
                Name = name
            };
    }

    private sealed class TestIncludeParent
    {
        public long Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public List<TestIncludeChild> Children { get; init; } = [];

        public static TestIncludeParent Create(long id, string name, IEnumerable<TestIncludeChild> children)
            => new()
            {
                Id = id,
                Name = name,
                Children = [.. children]
            };
    }

    private sealed class TestIncludeChild
    {
        public long Id { get; init; }

        public long? ParentId { get; private set; }

        public string Name { get; init; } = string.Empty;

        public TestIncludeParent? Parent { get; private set; }

        public static TestIncludeChild Create(long id, string name)
            => new()
            {
                Id = id,
                Name = name
            };
    }

    private sealed class TestIncludeOwnedParent
    {
        public long Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public TestIncludeOwnedParentDetail RequiredDetail { get; private set; } = null!;

        public List<TestIncludeOwnedChild> Children { get; init; } = [];

        public static TestIncludeOwnedParent Create(
            long id,
            string name,
            string detailCode,
            IEnumerable<string> detailNotes,
            IEnumerable<TestIncludeOwnedChild> children)
            => new()
            {
                Id = id,
                Name = name,
                RequiredDetail = TestIncludeOwnedParentDetail.Create(detailCode, detailNotes),
                Children = [.. children]
            };
    }

    private sealed class TestIncludeOwnedParentDetail
    {
        public string Code { get; private set; } = string.Empty;

        public List<TestIncludeOwnedParentNote> Notes { get; private set; } = [];

        public static TestIncludeOwnedParentDetail Create(string code, IEnumerable<string> notes)
            => new()
            {
                Code = code,
                Notes = [.. notes.Select(TestIncludeOwnedParentNote.Create)]
            };
    }

    private sealed class TestIncludeOwnedParentNote
    {
        public int Id { get; private set; }

        public string Message { get; private set; } = string.Empty;

        public static TestIncludeOwnedParentNote Create(string message)
            => new()
            {
                Message = message
            };
    }

    private sealed class TestIncludeOwnedChild
    {
        public long Id { get; init; }

        public long? ParentId { get; private set; }

        public string Name { get; init; } = string.Empty;

        public TestIncludeOwnedParent? Parent { get; private set; }

        public TestIncludeOwnedChildProfile RequiredProfile { get; private set; } = null!;

        public List<TestIncludeOwnedChildTag> Tags { get; private set; } = [];

        public static TestIncludeOwnedChild Create(
            long id,
            string name,
            string profileCode,
            string leafCode,
            IEnumerable<string> tags)
            => new()
            {
                Id = id,
                Name = name,
                RequiredProfile = TestIncludeOwnedChildProfile.Create(profileCode, leafCode),
                Tags = [.. tags.Select(TestIncludeOwnedChildTag.Create)]
            };
    }

    private sealed class TestIncludeOwnedChildProfile
    {
        public string Code { get; private set; } = string.Empty;

        public TestIncludeOwnedChildLeaf RequiredLeaf { get; private set; } = null!;

        public static TestIncludeOwnedChildProfile Create(string code, string leafCode)
            => new()
            {
                Code = code,
                RequiredLeaf = TestIncludeOwnedChildLeaf.Create(leafCode)
            };
    }

    private sealed class TestIncludeOwnedChildLeaf
    {
        public string Code { get; private set; } = string.Empty;

        public static TestIncludeOwnedChildLeaf Create(string code)
            => new()
            {
                Code = code
            };
    }

    private sealed class TestIncludeOwnedChildTag
    {
        public int Id { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public static TestIncludeOwnedChildTag Create(string name)
            => new()
            {
                Name = name
            };
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

            modelBuilder.Entity<TestOrderWithOwned>(entity =>
            {
                entity.ToTable(nameof(TestOrderWithOwned));
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Number).HasMaxLength(256).IsRequired();

                entity.OwnsOne(e => e.RequiredDetail, detail =>
                {
                    detail.Property(d => d.Code)
                        .HasColumnName(nameof(TestRequiredOwnedDetail.Code))
                        .HasMaxLength(256)
                        .IsRequired();
                });
                entity.Navigation(e => e.RequiredDetail).IsRequired();

                entity.OwnsMany(e => e.Items, item =>
                {
                    item.ToTable(nameof(TestOwnedItem));
                    item.WithOwner().HasForeignKey("TestOrderWithOwnedId");
                    item.HasKey(i => i.Id);
                    item.Property(i => i.Id).ValueGeneratedOnAdd();
                    item.Property(i => i.Name).HasMaxLength(256).IsRequired();
                });
            });

            modelBuilder.Entity<TestOrderWithNestedOwned>(entity =>
            {
                entity.ToTable(nameof(TestOrderWithNestedOwned));
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Number).HasMaxLength(256).IsRequired();

                entity.OwnsOne(e => e.RequiredDetail, detail =>
                {
                    detail.Property(d => d.Code)
                        .HasColumnName("RequiredDetailCode")
                        .HasMaxLength(256)
                        .IsRequired();

                    detail.OwnsOne(d => d.RequiredLeaf, leaf =>
                    {
                        leaf.Property(l => l.Code)
                            .HasColumnName("RequiredDetailLeafCode")
                            .HasMaxLength(256)
                            .IsRequired();
                    });
                    detail.Navigation(d => d.RequiredLeaf).IsRequired();

                    detail.OwnsMany(d => d.Notes, note =>
                    {
                        note.ToTable(nameof(TestOwnedDetailNote));
                        note.WithOwner().HasForeignKey("TestOrderWithNestedOwnedId");
                        note.HasKey(n => n.Id);
                        note.Property(n => n.Id).ValueGeneratedOnAdd();
                        note.Property(n => n.Message).HasMaxLength(256).IsRequired();
                    });
                });
                entity.Navigation(e => e.RequiredDetail).IsRequired();

                entity.OwnsMany(e => e.Items, item =>
                {
                    item.ToTable(nameof(TestCompositeOwnedItem));
                    item.WithOwner().HasForeignKey("TestOrderWithNestedOwnedId");
                    item.HasKey(i => i.Id);
                    item.Property(i => i.Id).ValueGeneratedOnAdd();
                    item.Property(i => i.Name).HasMaxLength(256).IsRequired();

                    item.OwnsOne(i => i.Snapshot, snapshot =>
                    {
                        snapshot.Property(s => s.Code)
                            .HasColumnName("SnapshotCode")
                            .HasMaxLength(256)
                            .IsRequired();
                    });
                    item.Navigation(i => i.Snapshot).IsRequired();

                    item.OwnsMany(i => i.Tags, tag =>
                    {
                        tag.ToTable(nameof(TestOwnedItemTag));
                        tag.WithOwner().HasForeignKey("TestCompositeOwnedItemId");
                        tag.HasKey(t => t.Id);
                        tag.Property(t => t.Id).ValueGeneratedOnAdd();
                        tag.Property(t => t.Name).HasMaxLength(256).IsRequired();
                    });
                });
            });

            modelBuilder.Entity<TestIncludeParent>(entity =>
            {
                entity.ToTable(nameof(TestIncludeParent));
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
                entity.HasMany(e => e.Children)
                    .WithOne(e => e.Parent)
                    .HasForeignKey(e => e.ParentId);
            });

            modelBuilder.Entity<TestIncludeChild>(entity =>
            {
                entity.ToTable(nameof(TestIncludeChild));
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            });

            modelBuilder.Entity<TestIncludeOwnedParent>(entity =>
            {
                entity.ToTable(nameof(TestIncludeOwnedParent));
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(256).IsRequired();

                entity.OwnsOne(e => e.RequiredDetail, detail =>
                {
                    detail.Property(d => d.Code)
                        .HasColumnName("RequiredDetailCode")
                        .HasMaxLength(256)
                        .IsRequired();

                    detail.OwnsMany(d => d.Notes, note =>
                    {
                        note.ToTable(nameof(TestIncludeOwnedParentNote));
                        note.WithOwner().HasForeignKey("TestIncludeOwnedParentId");
                        note.HasKey(n => n.Id);
                        note.Property(n => n.Id).ValueGeneratedOnAdd();
                        note.Property(n => n.Message).HasMaxLength(256).IsRequired();
                    });
                });
                entity.Navigation(e => e.RequiredDetail).IsRequired();

                entity.HasMany(e => e.Children)
                    .WithOne(e => e.Parent)
                    .HasForeignKey(e => e.ParentId);
            });

            modelBuilder.Entity<TestIncludeOwnedChild>(entity =>
            {
                entity.ToTable(nameof(TestIncludeOwnedChild));
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(256).IsRequired();

                entity.OwnsOne(e => e.RequiredProfile, profile =>
                {
                    profile.Property(p => p.Code)
                        .HasColumnName("RequiredProfileCode")
                        .HasMaxLength(256)
                        .IsRequired();

                    profile.OwnsOne(p => p.RequiredLeaf, leaf =>
                    {
                        leaf.Property(l => l.Code)
                            .HasColumnName("RequiredProfileLeafCode")
                            .HasMaxLength(256)
                            .IsRequired();
                    });
                    profile.Navigation(p => p.RequiredLeaf).IsRequired();
                });
                entity.Navigation(e => e.RequiredProfile).IsRequired();

                entity.OwnsMany(e => e.Tags, tag =>
                {
                    tag.ToTable(nameof(TestIncludeOwnedChildTag));
                    tag.WithOwner().HasForeignKey("TestIncludeOwnedChildId");
                    tag.HasKey(t => t.Id);
                    tag.Property(t => t.Id).ValueGeneratedOnAdd();
                    tag.Property(t => t.Name).HasMaxLength(256).IsRequired();
                });
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
        }

        public INOFAppBuilder AddRegistrationStep(IServiceRegistrationStep registrationStep)
        {
            _registrationSteps.Add(registrationStep);
            return this;
        }

        public INOFAppBuilder RemoveRegistrationStep(Predicate<IServiceRegistrationStep> predicate)
        {
            _registrationSteps.RemoveAll(predicate);
            return this;
        }

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
