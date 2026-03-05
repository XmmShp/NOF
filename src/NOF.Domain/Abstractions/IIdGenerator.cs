namespace NOF.Domain;

/// <summary>
/// Generates unique 64-bit identifiers.
/// </summary>
public interface IIdGenerator
{
    /// <summary>Generates the next unique ID.</summary>
    long NextId();
}
