using FluentAssertions;
using NOF.Application;
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
    public void CloneEntity_WithUnsupportedType_ShouldThrow()
    {
        var store = new MemoryPersistenceStore();

        var act = () => store.CloneEntity(new UnsupportedEntity());

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Cloning is not supported*");
    }

    [Fact]
    public void CaptureSnapshot_AndRestoreSnapshot_ShouldRestoreCustomPartitionState()
    {
        var store = new MemoryPersistenceStore();
        var partition = store.GetPartition<TestProjection, long>("custom", static item => item.Id, static item => new TestProjection { Id = item.Id, Name = item.Name });
        partition.Items[1] = new TestProjection { Id = 1, Name = "before" };

        var snapshot = store.CaptureSnapshot();

        partition.Items[1] = new TestProjection { Id = 1, Name = "after" };
        store.RestoreSnapshot(snapshot);

        var restored = store.GetPartition<TestProjection, long>("custom", static item => item.Id, static item => new TestProjection { Id = item.Id, Name = item.Name });
        restored.Items[1].Name.Should().Be("before");
    }

    [Fact]
    public void CloneEntity_ShouldCloneBuiltInEntities()
    {
        var store = new MemoryPersistenceStore();

        store.CloneEntity(new NOFInboxMessage(Guid.NewGuid())).Should().NotBeNull();
        store.CloneEntity(new NOFTenant { Id = "tenant-1", Name = "Tenant" }).Should().NotBeNull();
        store.CloneEntity(new NOFOutboxMessage { Id = 1, Payload = "{}", Headers = "{}", PayloadType = typeof(string).AssemblyQualifiedName!, MessageType = OutboxMessageType.Command }).Should().NotBeNull();
        store.CloneEntity(new NOFStateMachineContext { CorrelationId = "corr", DefinitionTypeName = "def", State = 1 }).Should().NotBeNull();
    }

    private sealed class TestProjection
    {
        public long Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    private sealed class UnsupportedEntity;
}
