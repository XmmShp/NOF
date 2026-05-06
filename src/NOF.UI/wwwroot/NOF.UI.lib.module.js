const browserInfo = {
    get() {
        const navigatorInfo = window.navigator ?? {};
        const screenInfo = window.screen ?? {};

        return {
            userAgent: navigatorInfo.userAgent ?? "",
            platform: navigatorInfo.userAgentData?.platform ?? navigatorInfo.platform ?? "",
            vendor: navigatorInfo.vendor ?? "",
            language: navigatorInfo.language ?? "",
            languages: navigatorInfo.languages ?? [],
            timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone ?? "",
            cookieEnabled: navigatorInfo.cookieEnabled ?? false,
            isOnline: navigatorInfo.onLine ?? false,
            hardwareConcurrency: navigatorInfo.hardwareConcurrency ?? 0,
            devicePixelRatio: window.devicePixelRatio ?? 1,
            maxTouchPoints: navigatorInfo.maxTouchPoints ?? 0,
            viewport: {
                width: window.innerWidth ?? 0,
                height: window.innerHeight ?? 0
            },
            screen: {
                width: screenInfo.width ?? 0,
                height: screenInfo.height ?? 0,
                availableWidth: screenInfo.availWidth ?? 0,
                availableHeight: screenInfo.availHeight ?? 0,
                colorDepth: screenInfo.colorDepth ?? 0,
                pixelDepth: screenInfo.pixelDepth ?? 0
            }
        };
    }
};

globalThis.NOF ??= {};
globalThis.NOF.UI ??= {};
globalThis.NOF.UI.browserInfo = browserInfo;

export function afterWebStarted() {
}

export function afterStarted() {
}
