namespace NOF.UI;

public sealed class BrowserInfoChangedEventArgs(BrowserInfoChangeKind changeKind, BrowserInfo browserInfo) : EventArgs
{
    public BrowserInfoChangeKind ChangeKind { get; } = changeKind;

    public BrowserInfo BrowserInfo { get; } = browserInfo;
}
