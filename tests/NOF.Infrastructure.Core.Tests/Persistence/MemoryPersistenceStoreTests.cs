using FluentAssertions;
using NOF.Infrastructure.Memory;
using Xunit;

namespace NOF.Infrastructure.Core.Tests.Persistence;

public class MemoryPersistenceStoreTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("tenant-a", "tenant-a")]
    public void NormalizeTenantId_ShouldNormalizeExpectedValue(string? tenantId, string expected)
    {
        var normalized = MemoryPersistenceStore.NormalizeTenantId(tenantId);

        normalized.Should().Be(expected);
    }

    [Fact]
    public void Clone_ShouldCreateDeepCopyOfTenantData()
    {
        var store = new MemoryPersistenceStore();
        var context = store.CreateContext("tenant-a");
        context.Set<TestProjection>().Add(new TestProjection { Id = 1, Name = "before" });

        var clone = (MemoryPersistenceStore)store.Clone();
        var clonedContext = clone.CreateContext("tenant-a");
        clonedContext.Set<TestProjection>()[0].Name = "after";

        context.Set<TestProjection>()[0].Name.Should().Be("before");
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
        restored[0].Name.Should().Be("before");
    }

    [Fact]
    public void CreateContext_ShouldKeepDataIsolatedByTenant()
    {
        var store = new MemoryPersistenceStore();
        var host = store.CreateContext(null);
        var tenant = store.CreateContext("tenant-a");

        host.Set<TestProjection>().Add(new TestProjection { Id = 1, Name = "host" });
        tenant.Set<TestProjection>().Add(new TestProjection { Id = 2, Name = "tenant" });

        host.Set<TestProjection>().Should().ContainSingle(item => item.Name == "host");
        tenant.Set<TestProjection>().Should().ContainSingle(item => item.Name == "tenant");
    }

    private sealed class TestProjection : ICloneable
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public object Clone()
            => new TestProjection { Id = Id, Name = Name };
    }
}
