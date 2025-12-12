namespace NOF.Sample;

public record ConfigNodeParentUpdatedEvent(
    ConfigNodeId NodeId,
    ConfigNodeId? OldParentId,
    ConfigNodeId? NewParentId) : IEvent;
