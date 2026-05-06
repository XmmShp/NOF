const browserInfoListeners = new Map();

function getBrowserInfo() {
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

function notifyListener(dotNetObjectReference, changeKind) {
    return dotNetObjectReference.invokeMethodAsync("NotifyChanged", changeKind, getBrowserInfo());
}

function subscribeBrowserInfo(listenerId, dotNetObjectReference) {
    if (!listenerId || !dotNetObjectReference || browserInfoListeners.has(listenerId)) {
        return;
    }

    let isLandscape = window.innerWidth >= window.innerHeight;
    const resizeHandler = () => {
        const nextIsLandscape = window.innerWidth >= window.innerHeight;
        const changeKind = nextIsLandscape !== isLandscape ? "OrientationChange" : "Resize";
        isLandscape = nextIsLandscape;
        void notifyListener(dotNetObjectReference, changeKind);
    };
    const onlineHandler = () => void notifyListener(dotNetObjectReference, "Online");
    const offlineHandler = () => void notifyListener(dotNetObjectReference, "Offline");

    window.addEventListener("resize", resizeHandler);
    window.addEventListener("online", onlineHandler);
    window.addEventListener("offline", offlineHandler);

    browserInfoListeners.set(listenerId, {
        dotNetObjectReference,
        resizeHandler,
        onlineHandler,
        offlineHandler
    });
}

function unsubscribeBrowserInfo(listenerId) {
    const listener = browserInfoListeners.get(listenerId);
    if (!listener) {
        return;
    }

    window.removeEventListener("resize", listener.resizeHandler);
    window.removeEventListener("online", listener.onlineHandler);
    window.removeEventListener("offline", listener.offlineHandler);
    browserInfoListeners.delete(listenerId);
}

const browserInfo = {
    get: getBrowserInfo,
    subscribe: subscribeBrowserInfo,
    unsubscribe: unsubscribeBrowserInfo
};

globalThis.NOF ??= {};
globalThis.NOF.UI ??= {};
globalThis.NOF.UI.browserInfo = browserInfo;

export function afterWebStarted() {
}

export function afterStarted() {
}
