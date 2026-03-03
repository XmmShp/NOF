using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers <see cref="IMapper"/> as a singleton backed by <see cref="ManualMapper"/>,
/// seeded from <see cref="MapperOptions"/> configured via the Options pattern.
/// <para>
/// Pre-build: <c>services.Configure&lt;MapperOptions&gt;(o =&gt; o.Add&lt;A, B&gt;(...))</c>.
/// Runtime: inject <see cref="IMapper"/> and call <c>Add</c> / <c>Map</c> as needed.
/// </para>
/// </summary>
internal sealed class MapperRegistrationStep : IServiceRegistrationStep<MapperRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.AddSingleton<IMapper>(sp =>
            new ManualMapper(sp.GetRequiredService<IOptions<MapperOptions>>()));
        return ValueTask.CompletedTask;
    }
}
