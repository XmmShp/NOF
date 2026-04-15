using System.Diagnostics.CodeAnalysis;

namespace NOF.Annotation;

/// <summary>
/// Represents one source-generated auto-inject registration item.
/// </summary>
public sealed record AutoInjectServiceRegistration(
    Type ServiceType,
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type ImplementationType,
    Lifetime Lifetime,
    bool UseFactory);
