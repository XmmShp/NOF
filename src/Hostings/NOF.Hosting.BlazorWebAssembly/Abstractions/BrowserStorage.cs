using Microsoft.JSInterop;

namespace NOF.Hosting.BlazorWebAssembly;

public abstract class BrowserStorage(IJSRuntime jsRuntime) : IBrowserStorage
{
    public ValueTask<string?> GetItemAsync(string key)
        => jsRuntime.InvokeAsync<string?>(StorageName + ".getItem", key);

    public ValueTask SetItemAsync(string key, string value)
        => jsRuntime.InvokeVoidAsync(StorageName + ".setItem", key, value);

    public ValueTask RemoveItemAsync(string key)
        => jsRuntime.InvokeVoidAsync(StorageName + ".removeItem", key);

    protected abstract string StorageName { get; }
}
