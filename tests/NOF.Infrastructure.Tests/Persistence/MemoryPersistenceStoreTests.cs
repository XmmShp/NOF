using NOF.Infrastructure.Memory;
using Xunit;

namespace NOF.Infrastructure.Tests.Persistence;

public class MemoryPersistenceStoreTests
{
    [Fact]
    public void Clone_ShouldCreateDeepCopyOfTenantData()
    {
        var store = new MemoryPersistenceStore();
        var context = store.CreateContext("tenant-a");
        context.Set<TestProjection>().Add(new TestProjection { Id = 1, Name = "before" });

        var clone = (MemoryPersistenceStore)store.Clone();
        var clonedContext = clone.CreateContext("tenant-a");
        clonedContext.Set<TestProjection>()[0].Name = "after";
        Assert.Equal("before",

        context.Set<TestProjection>()[0].Name);
    }

    [Fact]
    public void RestoreFrom_ShouldRestoreSnapshotState()
    {
        var store = new MemoryPersistenceStore();
        var context = store.CreateContext("tenant-a");
        context.Set<TestProjection>().Add(new TestProjection { Id = 1, Name = "before" });

        var snapshot = (MemoryPersistenceStore)store.Clone();

        context.Set<TestProjection>()[0].Name = "after";
        store.RestoreFrom(snapshot);

        var restored = store.CreateContext("tenant-a").Set<TestProjection>();
        Assert.Equal("before",
        restored[0].Name);
    }

    [Fact]
    public void CreateContext_ShouldKeepDataIsolatedByTenant()
    {
        var store = new MemoryPersistenceStore();
        var host = store.CreateContext(string.Empty);
        var tenant = store.CreateContext("tenant-a");

        host.Set<TestProjection>().Add(new TestProjection { Id = 1, Name = "host" });
        tenant.Set<TestProjection>().Add(new TestProjection { Id = 2, Name = "tenant" });

        Assert.Single(host.Set<TestProjection>(), item => item.Name == "host");
        Assert.Single(tenant.Set<TestProjection>(), item => item.Name == "tenant");
    }

    private sealed class TestProjection : ICloneable
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public object Clone()
            => new TestProjection { Id = Id, Name = Name };
    }
}

