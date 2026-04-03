using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Hosting;
using NOF.UI;

namespace NOF.Hosting.BlazorWebAssembly;

/// <summary>
/// Registers built-in browser storage primitives for Blazor WebAssembly hosting.
/// </summary>
public sealed class BrowserStorageRegistrationStep : IServiceRegistrationStep<BrowserStorageRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.TryAddScoped<ILocalStorage, LocalStorage>();
        builder.Services.TryAddScoped<ISessionStorage, SessionStorage>();
        return ValueTask.CompletedTask;
    }
}
