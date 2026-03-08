using Microsoft.JSInterop;

namespace NOF.Hosting.BlazorWebAssembly;

public sealed class SessionStorage(IJSRuntime jsRuntime) : BrowserStorage(jsRuntime), ISessionStorage
{
    protected override string StorageName => "sessionStorage";
}
