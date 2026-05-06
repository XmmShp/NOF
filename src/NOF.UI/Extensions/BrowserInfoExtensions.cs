namespace NOF.UI;

public static partial class BrowserInfoExtensions
{
    extension(BrowserInfo browserInfo)
    {
        public bool IsTouchDevice()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            return browserInfo.MaxTouchPoints > 0;
        }

        public bool IsMobile()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            var userAgent = GetNormalizedUserAgent(browserInfo);
            return userAgent.Contains("iphone", StringComparison.Ordinal)
                || userAgent.Contains("ipod", StringComparison.Ordinal)
                || userAgent.Contains("windows phone", StringComparison.Ordinal)
                || userAgent.Contains("mobile", StringComparison.Ordinal)
                || (userAgent.Contains("android", StringComparison.Ordinal) && userAgent.Contains("mobile", StringComparison.Ordinal));
        }

        public bool IsTablet()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            var userAgent = GetNormalizedUserAgent(browserInfo);
            return userAgent.Contains("ipad", StringComparison.Ordinal)
                || userAgent.Contains("tablet", StringComparison.Ordinal)
                || (userAgent.Contains("android", StringComparison.Ordinal) && !userAgent.Contains("mobile", StringComparison.Ordinal));
        }

        public bool IsDesktop()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            return !browserInfo.IsMobile() && !browserInfo.IsTablet();
        }

        public bool IsWindows()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            return GetNormalizedPlatform(browserInfo).Contains("win", StringComparison.Ordinal)
                || GetNormalizedUserAgent(browserInfo).Contains("windows", StringComparison.Ordinal);
        }

        public bool IsMacOS()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            var platform = GetNormalizedPlatform(browserInfo);
            var userAgent = GetNormalizedUserAgent(browserInfo);
            return (platform.Contains("mac", StringComparison.Ordinal) || userAgent.Contains("mac os", StringComparison.Ordinal))
                && !browserInfo.IsIOS();
        }

        public bool IsLinux()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            var platform = GetNormalizedPlatform(browserInfo);
            var userAgent = GetNormalizedUserAgent(browserInfo);
            return platform.Contains("linux", StringComparison.Ordinal)
                || userAgent.Contains("linux", StringComparison.Ordinal)
                || userAgent.Contains("x11", StringComparison.Ordinal);
        }

        public bool IsAndroid()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            return GetNormalizedUserAgent(browserInfo).Contains("android", StringComparison.Ordinal);
        }

        public bool IsIOS()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            var userAgent = GetNormalizedUserAgent(browserInfo);
            var platform = GetNormalizedPlatform(browserInfo);
            return userAgent.Contains("iphone", StringComparison.Ordinal)
                || userAgent.Contains("ipad", StringComparison.Ordinal)
                || userAgent.Contains("ipod", StringComparison.Ordinal)
                || ((platform.Contains("mac", StringComparison.Ordinal) || platform.Contains("ios", StringComparison.Ordinal))
                    && browserInfo.IsTouchDevice()
                    && userAgent.Contains("applewebkit", StringComparison.Ordinal));
        }

        public bool IsPortrait()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            return browserInfo.Viewport.Height > browserInfo.Viewport.Width;
        }

        public bool IsLandscape()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            return browserInfo.Viewport.Width >= browserInfo.Viewport.Height;
        }

        public bool HasViewportAtLeast(int width, int height)
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            return browserInfo.Viewport.Width >= width && browserInfo.Viewport.Height >= height;
        }

        public bool IsSmallViewport()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            return browserInfo.Viewport.Width < 768;
        }

        public bool IsMediumViewport()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            return browserInfo.Viewport.Width >= 768 && browserInfo.Viewport.Width < 1280;
        }

        public bool IsLargeViewport()
        {
            ArgumentNullException.ThrowIfNull(browserInfo);
            return browserInfo.Viewport.Width >= 1280;
        }
    }

    private static string GetNormalizedUserAgent(BrowserInfo browserInfo)
        => browserInfo.UserAgent.ToLowerInvariant();

    private static string GetNormalizedPlatform(BrowserInfo browserInfo)
        => browserInfo.Platform.ToLowerInvariant();
}
