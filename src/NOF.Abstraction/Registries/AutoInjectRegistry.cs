using Microsoft.Extensions.DependencyInjection;

namespace NOF.Abstraction;

/// <summary>
/// Registry of auto-inject service descriptors.
/// </summary>
public sealed class AutoInjectRegistry : Registry<ServiceDescriptor>;
