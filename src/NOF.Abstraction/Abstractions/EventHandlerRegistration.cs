using System.Diagnostics.CodeAnalysis;

namespace NOF.Abstraction;

/// <summary>
/// Source-generated registration entry for an in-memory event handler.
/// </summary>
public sealed record EventHandlerRegistration(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerType,
    Type EventType);
