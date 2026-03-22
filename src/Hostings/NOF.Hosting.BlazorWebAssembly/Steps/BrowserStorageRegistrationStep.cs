using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Infrastructure;

namespace NOF.Hosting.BlazorWebAssembly;

/// <summary>
/// Registers built-in browser storage primitives for Blazor WebAssembly hosting.
/// </summary>
public sealed class BrowserStorageRegistrationStep : IBaseSettingsServiceRegistrationStep<BrowserStorageRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.TryAddScoped<ILocalStorage, LocalStorage>();
        builder.Services.TryAddScoped<ISessionStorage, SessionStorage>();
        return ValueTask.CompletedTask;
    }
}
