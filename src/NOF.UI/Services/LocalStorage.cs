using Microsoft.JSInterop;

namespace NOF.UI;

public sealed class LocalStorage(IJSRuntime jsRuntime) : ILocalStorage
{
    public ValueTask<string?> GetItemAsync(string key)
        => jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);

    public ValueTask SetItemAsync(string key, string value)
        => jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);

    public ValueTask RemoveItemAsync(string key)
        => jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
}
