namespace NOF.Abstraction;

/// <summary>
/// Tracks which generated assembly initializers have already run for the current service collection.
/// </summary>
public sealed class InitializedTypes : HashSet<Type>;
