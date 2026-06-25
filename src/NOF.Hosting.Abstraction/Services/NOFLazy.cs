using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

/// <summary>
/// Default <see cref="Lazy{T}"/> implementation backed by the current DI scope.
/// </summary>
public sealed class NOFLazy<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : Lazy<T>
    where T : notnull
{
    public NOFLazy(IServiceProvider serviceProvider)
        : base(serviceProvider.GetRequiredService<T>)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
    }
}
