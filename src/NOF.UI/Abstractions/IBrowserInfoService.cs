namespace NOF.UI;

public interface IBrowserInfoService
{
    ValueTask<BrowserInfo> GetAsync(CancellationToken cancellationToken = default);

    ValueTask<BrowserInfo> RefreshAsync(CancellationToken cancellationToken = default);
}
