using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Application;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Sets <see cref="Mapper.Current"/> from the DI container after the host is built.
/// Added to the builder by default.
/// </summary>
internal sealed class MapperInitializationStep : IApplicationInitializationStep<MapperInitializationStep>
{
    public Task ExecuteAsync(IApplicationInitializationContext context, IHost app)
    {
        var mapper = app.Services.GetRequiredService<IMapper>();
        Mapper.SetCurrent(mapper);
        return Task.CompletedTask;
    }
}
