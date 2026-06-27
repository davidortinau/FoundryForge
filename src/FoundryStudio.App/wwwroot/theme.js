// FoundryStudio in-app theme control. The design tokens (tokens.css) already define light
// (:root / [data-theme="light"]) and dark (prefers-color-scheme: dark AND [data-theme="dark"]).
// This persists an explicit choice and applies it to <html> so the user can override the system theme.
// "system" removes the attribute so prefers-color-scheme drives the tokens again.
(function () {
    const KEY = "fs-theme";
    const VALID = ["system", "light", "dark"];

    function apply(value) {
        if (!VALID.includes(value)) {
            value = "system";
        }
        const root = document.documentElement;
        if (value === "system") {
            root.removeAttribute("data-theme");
        } else {
            root.setAttribute("data-theme", value);
        }
        try { localStorage.setItem(KEY, value); } catch { /* storage may be unavailable */ }
        syncNativeWindow(value);
        return value;
    }

    function current() {
        try {
            const v = localStorage.getItem(KEY);
            return VALID.includes(v) ? v : "system";
        } catch {
            return "system";
        }
    }

    function pushBackground() {
        try {
            var bg = getComputedStyle(document.body).backgroundColor;
            if (window.DotNet && bg) {
                window.DotNet.invokeMethodAsync('FoundryStudio.App', 'SetWindowBackgroundColor', bg);
            }
        } catch (e) { /* interop not ready yet */ }
    }

    function syncNativeWindow(mode, attempt) {
        // Drive the native window from the in-app theme. Order matters:
        //  1. Set the window APPEARANCE from the mode. "system" clears the forced appearance so the
        //     window inherits the OS theme and the WebView's prefers-color-scheme reports correctly.
        //  2. THEN push the body canvas color so the titlebar band matches. For "system" the body color
        //     depends on the (just-cleared) appearance, so push a few times to catch the recompute.
        // Blazor's DotNet interop may not be ready at first paint, so retry briefly until it is.
        attempt = attempt || 0;
        try {
            if (window.DotNet) {
                window.DotNet.invokeMethodAsync('FoundryStudio.App', 'SetWindowThemeMode', mode);
                pushBackground();
                setTimeout(pushBackground, 120);
                setTimeout(pushBackground, 300);
                return;
            }
        } catch (e) { /* interop not ready yet */ }
        if (attempt < 40) {
            setTimeout(function () { syncNativeWindow(mode, attempt + 1); }, 100);
        }
    }

    function syncWindowBackground() { pushBackground(); }

    window.foundryStudioTheme = { apply, current, syncWindowBackground };

    // Apply the persisted choice immediately (the inline head script already did a first pass to avoid a
    // flash; this re-applies once this script loads, in case it ran later).
    apply(current());
})();
