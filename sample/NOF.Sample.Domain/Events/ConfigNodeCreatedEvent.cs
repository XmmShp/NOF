namespace NOF.Sample;

public record ConfigNodeCreatedEvent(ConfigNodeId Id, ConfigNodeName Name, ConfigNodeId? ParentId) : IEvent;
