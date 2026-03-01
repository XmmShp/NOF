namespace NOF.Application;

/// <summary>
/// Identifies a mapping registration: source type, destination type, and optional name.
/// </summary>
/// <param name="Source">The source type.</param>
/// <param name="Destination">The destination type.</param>
/// <param name="Name">Optional mapping name. <see langword="null"/> = default (unnamed) mapping.</param>
public sealed record MapKey(Type Source, Type Destination, string? Name = null);
