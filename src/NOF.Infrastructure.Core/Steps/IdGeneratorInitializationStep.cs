using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Domain;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Sets <see cref="IdGenerator.Current"/> from the DI container after the host is built.
/// Added to the builder by default.
/// </summary>
internal sealed class IdGeneratorInitializationStep : IApplicationInitializationStep<IdGeneratorInitializationStep>
{
    public Task ExecuteAsync(IHostApplicationBuilder context, IHost app)
    {
        var generator = app.Services.GetRequiredService<IIdGenerator>();
        IdGenerator.SetCurrent(generator);
        return Task.CompletedTask;
    }
}
