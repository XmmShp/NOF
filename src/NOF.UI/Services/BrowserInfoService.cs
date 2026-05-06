using Microsoft.JSInterop;

namespace NOF.UI;

public sealed class BrowserInfoService : IBrowserInfoService, IAsyncDisposable
{
    private const string GetBrowserInfoIdentifier = "NOF.UI.browserInfo.get";
    private const string SubscribeBrowserInfoIdentifier = "NOF.UI.browserInfo.subscribe";
    private const string UnsubscribeBrowserInfoIdentifier = "NOF.UI.browserInfo.unsubscribe";
    private readonly IJSRuntime _jsRuntime;
    private readonly string _listenerId = Guid.NewGuid().ToString("N");
    private BrowserInfo? _cachedInfo;
    private DotNetObjectReference<BrowserInfoService>? _dotNetObjectReference;
    private Task? _subscriptionTask;
    private bool _isListening;

    public BrowserInfoService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
        _subscriptionTask = TryStartListeningAsync().AsTask();
    }

    public event EventHandler<BrowserInfoChangedEventArgs>? Changed;

    public async ValueTask<BrowserInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureListeningAsync(cancellationToken);

        if (_cachedInfo is not null)
        {
            return _cachedInfo;
        }

        return await RefreshAsync(cancellationToken);
    }

    public async ValueTask<BrowserInfo> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await EnsureListeningAsync(cancellationToken);
        _cachedInfo = await _jsRuntime.InvokeAsync<BrowserInfo>(GetBrowserInfoIdentifier, cancellationToken);
        return _cachedInfo ?? new BrowserInfo();
    }

    [JSInvokable]
    public Task NotifyChanged(string? changeKind, BrowserInfo? browserInfo)
    {
        _cachedInfo = browserInfo ?? new BrowserInfo();
        Changed?.Invoke(this, new BrowserInfoChangedEventArgs(ParseChangeKind(changeKind), _cachedInfo));
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopListeningAsync();
        }
        catch (JSDisconnectedException)
        {
        }

        _dotNetObjectReference?.Dispose();
    }

    private static BrowserInfoChangeKind ParseChangeKind(string? changeKind)
        => Enum.TryParse<BrowserInfoChangeKind>(changeKind, true, out var parsedChangeKind)
            ? parsedChangeKind
            : BrowserInfoChangeKind.Unknown;

    private async ValueTask EnsureListeningAsync(CancellationToken cancellationToken = default)
    {
        if (_isListening)
        {
            return;
        }

        if (_subscriptionTask is null || _subscriptionTask.IsCompleted)
        {
            _subscriptionTask = TryStartListeningAsync(cancellationToken).AsTask();
        }

        await _subscriptionTask;
    }

    private async ValueTask TryStartListeningAsync(CancellationToken cancellationToken = default)
    {
        if (_isListening)
        {
            return;
        }

        _dotNetObjectReference ??= DotNetObjectReference.Create(this);

        try
        {
            await _jsRuntime.InvokeVoidAsync(
                SubscribeBrowserInfoIdentifier,
                cancellationToken,
                _listenerId,
                _dotNetObjectReference);
            _isListening = true;
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async ValueTask StopListeningAsync(CancellationToken cancellationToken = default)
    {
        if (!_isListening)
        {
            return;
        }

        await _jsRuntime.InvokeVoidAsync(UnsubscribeBrowserInfoIdentifier, cancellationToken, _listenerId);
        _isListening = false;
    }
}
