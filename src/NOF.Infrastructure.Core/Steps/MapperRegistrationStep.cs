using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers <see cref="ManualMapper"/> as a singleton via <c>GetOrAddSingleton</c>
/// and aliases <see cref="IMapper"/> to the same instance.
/// <para>
/// Because <c>GetOrAddSingleton</c> is used, user code that runs before the provider is built
/// (e.g. extension methods on <see cref="INOFAppBuilder"/>) can grab the same instance
/// and call <see cref="IMapper.CreateMap{TSource,TDestination}"/> freely.
/// </para>
/// </summary>
internal sealed class MapperRegistrationStep : IServiceRegistrationStep
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        var config = builder.Services.GetOrAddSingleton<ManualMapper>();
        builder.Services.AddSingleton<IMapper>(config);
        return ValueTask.CompletedTask;
    }
}
