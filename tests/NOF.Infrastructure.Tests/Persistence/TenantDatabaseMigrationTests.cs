using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NOF.Contract;
using NOF.Hosting;
using NOF.Infrastructure.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Xunit;

namespace NOF.Infrastructure.Tests.Persistence;

public sealed class TenantDatabaseMigrationTests
{
    [Fact]
    public async Task Initialization_ShouldAwaitHostMigrationAndRestoreAmbientTenant()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = new Harness(async ct =>
        {
            Assert.Equal("host", Context.Current.TenantId);
            entered.SetResult();
            await release.Task.WaitAsync(ct);
        });
        using var ambient = Context.PushCurrent(Context.Empty.WithTenantId("caller"));
        var migration = harness.Step.ExecuteAsync(harness.Host);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(migration.IsCompleted);
        release.SetResult();
        await migration;
        Assert.Equal("caller", Context.Current.TenantId);
        Assert.Equal(["host"], harness.CreatedTenants);
    }

    [Fact]
    public async Task Initialization_ShouldPropagateHostFailure()
    {
        using var harness = new Harness(_ => Task.FromException(new InvalidOperationException("host failed")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Step.ExecuteAsync(harness.Host));
    }

    [Fact]
    public async Task Background_ShouldWaitForStartupDeduplicateAndContinueAfterTenantFailure()
    {
        var migrated = new ConcurrentQueue<string>();
        using var harness = new Harness(_ =>
        {
            var tenant = Context.Current.TenantId;
            migrated.Enqueue(tenant);
            return tenant == "bad" ? Task.FromException(new InvalidOperationException("tenant failed")) : Task.CompletedTask;
        });
        var catalog = new Catalog("host", " first ", "first", "bad", "last");
        using var service = harness.CreateService(catalog);
        await harness.Step.ExecuteAsync(harness.Host);
        await service.StartAsync(default);
        Assert.Equal(["host"], migrated);
        Assert.Equal(0, catalog.Calls);

        await harness.Host.StartAsync();
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(["host", "first", "bad", "last"], migrated);
        Assert.Equal(["host", "first", "bad", "last"], harness.CreatedTenants);
        Assert.Equal("host", catalog.AmbientTenant);
        await service.StopAsync(default);
    }

    [Fact]
    public async Task Background_ShouldNotBlockStartupAndShouldObserveShutdown()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = new Harness(async ct =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        });
        using var service = harness.CreateService(new Catalog("first", "second"));
        await service.StartAsync(default);
        await harness.Host.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(service.ExecuteTask!.IsCompleted);
        await service.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(["first"], harness.CreatedTenants);
        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task SharedDatabase_ShouldSkipTenantEnumeration()
    {
        using var harness = new Harness(_ => Task.CompletedTask);
        var catalog = new Catalog("first");
        using var service = harness.CreateService(catalog, TenantMode.SharedDatabase);
        await service.StartAsync(default);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, catalog.Calls);
        Assert.Empty(harness.CreatedTenants);
    }

    [Fact]
    public async Task CatalogFailure_ShouldNotFaultBackgroundService()
    {
        using var harness = new Harness(_ => Task.CompletedTask);
        var catalog = new Mock<ITenantProvider>();
        catalog.Setup(provider => provider.GetTenantIdsAsync(It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("catalog failed"));
        using var service = harness.CreateService(catalog.Object);
        await harness.Host.StartAsync();
        await service.StartAsync(default);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Empty(harness.CreatedTenants);
    }

    [Fact]
    public void RepeatedMigrateOnInitialize_ShouldRegisterOneStepAndWorker()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        var selector = builder.UseDbContext<NOFDbContext>();
        selector.MigrateOnInitialize().MigrateOnInitialize();
        Assert.Single(builder.Services, descriptor => descriptor.ServiceType == typeof(IApplicationInitializationStep));
        Assert.Single(builder.Services, descriptor => descriptor.ImplementationType == typeof(TenantDatabaseMigrationBackgroundService));
        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(ITenantProvider));
    }

    private sealed class Catalog(params string[] tenants) : ITenantProvider
    {
        public int Calls { get; private set; }
        public string? AmbientTenant { get; private set; }

        public async IAsyncEnumerable<string> GetTenantIdsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Calls++;
            AmbientTenant = Context.Current.TenantId;
            await Task.CompletedTask;
            foreach (var tenant in tenants)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return tenant;
            }
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _efServices;
        public IHost Host { get; }
        public DbContextMigrationInitializationStep Step { get; } = new(typeof(DbContext));
        public ConcurrentQueue<string> CreatedTenants { get; } = new();

        public Harness(Func<CancellationToken, Task> migrate)
        {
            var migrator = new Mock<IMigrator>();
            migrator.Setup(instance => instance.MigrateAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns((string? _, CancellationToken ct) => migrate(ct));
            _efServices = new ServiceCollection().AddEntityFrameworkSqlite().AddSingleton(migrator.Object).BuildServiceProvider();
            var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
            builder.Services.AddScoped(_ =>
            {
                CreatedTenants.Enqueue(Context.Current.TenantId);
                return new DbContext(new DbContextOptionsBuilder().UseSqlite("Data Source=:memory:")
                    .UseInternalServiceProvider(_efServices).Options);
            });
            Host = builder.Build();
        }

        public TenantDatabaseMigrationBackgroundService CreateService(ITenantProvider provider, TenantMode mode = TenantMode.DatabasePerTenant)
            => new(Host.Services, [Step], provider, Options.Create(new DbContextConfigurationOptions { TenantMode = mode }),
                Host.Services.GetRequiredService<IHostApplicationLifetime>(), NullLogger<TenantDatabaseMigrationBackgroundService>.Instance);

        public void Dispose()
        {
            Host.Dispose();
            _efServices.Dispose();
        }
    }
}
