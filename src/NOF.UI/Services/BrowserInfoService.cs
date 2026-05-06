using Microsoft.JSInterop;

namespace NOF.UI;

public sealed class BrowserInfoService(IJSRuntime jsRuntime) : IBrowserInfoService
{
    private const string GetBrowserInfoIdentifier = "NOF.UI.browserInfo.get";
    private BrowserInfo? _cachedInfo;

    public async ValueTask<BrowserInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedInfo is not null)
        {
            return _cachedInfo;
        }

        return await RefreshAsync(cancellationToken);
    }

    public async ValueTask<BrowserInfo> RefreshAsync(CancellationToken cancellationToken = default)
    {
        _cachedInfo = await jsRuntime.InvokeAsync<BrowserInfo>(GetBrowserInfoIdentifier, cancellationToken);
        return _cachedInfo ?? new BrowserInfo();
    }
}
