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

    window.foundryStudioTheme = { apply, current };

    // Apply the persisted choice immediately (the inline head script already did a first pass to avoid a
    // flash; this re-applies once this script loads, in case it ran later).
    apply(current());
})();
