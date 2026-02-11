using NOF.Domain;

namespace NOF.Sample;

public record ConfigNodeUpdatedEvent(ConfigNodeId Id, ConfigNodeName Name, ConfigNodeId? ParentId) : IEvent;
