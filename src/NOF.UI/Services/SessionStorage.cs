using Microsoft.JSInterop;

namespace NOF.UI;

public sealed class SessionStorage(IJSRuntime jsRuntime) : ISessionStorage
{
    public ValueTask<string?> GetItemAsync(string key)
        => jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", key);

    public ValueTask SetItemAsync(string key, string value)
        => jsRuntime.InvokeVoidAsync("sessionStorage.setItem", key, value);

    public ValueTask RemoveItemAsync(string key)
        => jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", key);
}
