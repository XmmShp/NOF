using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

/// <summary>
/// Default <see cref="Lazy{T}"/> implementation backed by the current DI scope.
/// </summary>
[UnconditionalSuppressMessage(
    "Trimming",
    "IL2091",
    Justification = "NOFLazy<T> resolves T via IServiceProvider factory instead of Lazy<T> parameterless-constructor activation.")]
public sealed class NOFLazy<T> : Lazy<T>
    where T : notnull
{
    public NOFLazy(IServiceProvider serviceProvider)
        : base(serviceProvider.GetRequiredService<T>)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
    }
}
