namespace NOF.UI;

public interface IBrowserInfoService
{
    event EventHandler<BrowserInfoChangedEventArgs>? Changed;

    ValueTask<BrowserInfo> GetAsync(CancellationToken cancellationToken = default);

    ValueTask<BrowserInfo> RefreshAsync(CancellationToken cancellationToken = default);
}
