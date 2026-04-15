namespace NOF.Domain;

/// <summary>
/// Represents an aggregate root entity that can raise domain events.
/// </summary>
public interface IAggregateRoot
{
    /// <summary>Gets the collection of uncommitted domain events.</summary>
    ICollection<object> Events { get; }
}
