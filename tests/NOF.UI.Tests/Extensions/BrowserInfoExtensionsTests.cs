using NOF.UI;
using Xunit;

namespace NOF.UI.Tests.Extensions;

public sealed class BrowserInfoExtensionsTests
{
    [Fact]
    public void IsDesktop_ShouldReturnTrue_ForWindowsDesktopBrowser()
    {
        var browserInfo = new BrowserInfo
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/136.0.0.0 Safari/537.36",
            Platform = "Win32",
            Viewport = new BrowserViewportInfo { Width = 1440, Height = 900 }
        };

        Assert.True(browserInfo.IsDesktop());
        Assert.True(browserInfo.IsWindows());
        Assert.True(browserInfo.IsLargeViewport());
    }

    [Fact]
    public void IsMobile_ShouldReturnTrue_ForIPhoneBrowser()
    {
        var browserInfo = new BrowserInfo
        {
            UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Version/18.0 Mobile/15E148 Safari/604.1",
            Platform = "iPhone",
            MaxTouchPoints = 5,
            Viewport = new BrowserViewportInfo { Width = 390, Height = 844 }
        };

        Assert.True(browserInfo.IsMobile());
        Assert.True(browserInfo.IsIOS());
        Assert.True(browserInfo.IsPortrait());
        Assert.True(browserInfo.IsSmallViewport());
    }

    [Fact]
    public void IsTablet_ShouldReturnTrue_ForAndroidTablet()
    {
        var browserInfo = new BrowserInfo
        {
            UserAgent = "Mozilla/5.0 (Linux; Android 14; Pixel Tablet) AppleWebKit/537.36 Chrome/136.0.0.0 Safari/537.36",
            Platform = "Linux armv8l",
            MaxTouchPoints = 10,
            Viewport = new BrowserViewportInfo { Width = 1280, Height = 800 }
        };

        Assert.True(browserInfo.IsTablet());
        Assert.True(browserInfo.IsAndroid());
        Assert.True(browserInfo.IsLandscape());
        Assert.True(browserInfo.HasViewportAtLeast(1024, 768));
    }
}
