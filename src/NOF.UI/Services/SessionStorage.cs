using Microsoft.JSInterop;

namespace NOF.UI;

public sealed class SessionStorage(IJSRuntime jsRuntime) : BrowserStorage(jsRuntime), ISessionStorage
{
    protected override string StorageName => "sessionStorage";
}

