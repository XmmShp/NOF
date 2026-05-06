namespace NOF.UI;

public sealed class BrowserInfo
{
    public string UserAgent { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string Vendor { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string[] Languages { get; set; } = [];

    public string TimeZone { get; set; } = string.Empty;

    public bool CookieEnabled { get; set; }

    public bool IsOnline { get; set; }

    public int HardwareConcurrency { get; set; }

    public decimal DevicePixelRatio { get; set; } = 1m;

    public int MaxTouchPoints { get; set; }

    public BrowserViewportInfo Viewport { get; set; } = new();

    public BrowserScreenInfo Screen { get; set; } = new();
}
