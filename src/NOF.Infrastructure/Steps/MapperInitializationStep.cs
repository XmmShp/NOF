using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Sets <see cref="Mapper.Current"/> from the DI container after the host is built.
/// Added to the builder by default.
/// </summary>
public sealed class MapperInitializationStep : IApplicationInitializationStep<MapperInitializationStep>
{
    public Task ExecuteAsync(IHostApplicationBuilder context, IHost app)
    {
        var mapper = app.Services.GetRequiredService<IMapper>();
        Mapper.SetCurrent(mapper);
        return Task.CompletedTask;
    }
}
