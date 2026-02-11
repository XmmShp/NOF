using NOF.Domain;

namespace NOF.Sample;

public record ConfigNodeDeletedEvent(ConfigNodeId Id, ConfigNodeId? ParentId) : IEvent;
