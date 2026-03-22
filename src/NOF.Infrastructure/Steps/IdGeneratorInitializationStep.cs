using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Domain;

namespace NOF.Infrastructure;

/// <summary>
/// Sets <see cref="IdGenerator.Current"/> from the DI container after the host is built.
/// Added to the builder by default.
/// </summary>
public sealed class IdGeneratorInitializationStep : IApplicationInitializationStep<IdGeneratorInitializationStep>
{
    public Task ExecuteAsync(IHostApplicationBuilder context, IHost app)
    {
        var generator = app.Services.GetRequiredService<IIdGenerator>();
        IdGenerator.SetCurrent(generator);
        return Task.CompletedTask;
    }
}
