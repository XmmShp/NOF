namespace NOF.Domain;

/// <summary>
/// Represents an aggregate root entity that can raise domain events.
/// </summary>
public interface IAggregateRoot : IEntity
{
    /// <summary>Gets the collection of uncommitted domain events.</summary>
    ICollection<IEvent> Events { get; }
}
