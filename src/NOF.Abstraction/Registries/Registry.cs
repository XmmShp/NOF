using NOF.Annotation;
using System.Collections.Concurrent;

namespace NOF.Abstraction;

/// <summary>
/// Centralized storage for source-generated registration metadata.
/// </summary>
public static class Registry
{
    public static ConcurrentBag<EventHandlerRegistration> EventHandlerRegistrations { get; } = [];

    public static ConcurrentBag<AutoInjectServiceRegistration> AutoInjectRegistrations { get; } = [];
}
