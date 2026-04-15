using System.Collections.Concurrent;
using NOF.Annotation;

namespace NOF.Abstraction;

/// <summary>
/// Centralized storage for source-generated registration metadata.
/// </summary>
public static class Registry
{
    public static ConcurrentBag<EventHandlerRegistration> EventHandlerRegistrations { get; } = [];

    public static ConcurrentBag<AutoInjectServiceRegistration> AutoInjectRegistrations { get; } = [];
}
