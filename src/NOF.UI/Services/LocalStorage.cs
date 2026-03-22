using Microsoft.JSInterop;

namespace NOF.UI;

public sealed class LocalStorage(IJSRuntime jsRuntime) : BrowserStorage(jsRuntime), ILocalStorage
{
    protected override string StorageName => "localStorage";
}

