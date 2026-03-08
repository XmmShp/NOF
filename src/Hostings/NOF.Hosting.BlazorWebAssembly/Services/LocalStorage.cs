using Microsoft.JSInterop;

namespace NOF.Hosting.BlazorWebAssembly;

public sealed class LocalStorage(IJSRuntime jsRuntime) : BrowserStorage(jsRuntime), ILocalStorage
{
    protected override string StorageName => "localStorage";
}
