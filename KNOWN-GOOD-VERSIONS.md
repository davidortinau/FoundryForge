# KNOWN-GOOD-VERSIONS.md

Frozen, known-good dependency set. Do **not** float to "latest." Any bump is a deliberate, isolated chore that re-runs the M0 gate (see `docs/PLAN.md`).

## Target toolchain (net11 — confirm in M0a via the `macos-maui-dogfood` skill)
| Component | net11 target | Status |
|---|---|---|
| .NET SDK band | net11.0-macos (preview); pin exact SDK band | **TBD in M0a** |
| maui-labs AppKit `Microsoft.Maui.Platforms.MacOS{,.BlazorWebView,.Essentials}` | net11-compatible build | **TBD in M0a** |
| `Microsoft.Maui.Controls` | 11.x preview (matched to AppKit build) | **TBD in M0a** |
| `Microsoft.AspNetCore.Components.WebView.Maui` | 11.x (matched) | **TBD in M0a** |
| `Microsoft.AI.Foundry.Local` | confirm `sdk` vs `sdk_v2`; pin exact | **TBD in M0b** |

## Proven reference baseline — MAUI.Sherpa (net10, known to work)
Use as the fallback reference and to validate the pattern. Source: [Redth/MAUI.Sherpa](https://github.com/Redth/MAUI.Sherpa) `src/MauiSherpa.MacOS`.

| Component | Version |
|---|---|
| .NET SDK (`global.json`) | 10.0.301 (rollForward latestMajor, allowPrerelease) |
| TargetFramework | net10.0-macos, SupportedOSPlatformVersion 14.0 |
| `Microsoft.Maui.Platforms.MacOS{,.BlazorWebView,.Essentials}` | 0.1.0-preview.8.26256.5 |
| `Microsoft.Maui.Controls` | 10.0.41 |
| `Microsoft.AspNetCore.Components.WebView.Maui` | 10.0.1 |
| `Microsoft.Maui.DevFlow.Agent` / `.Blazor` (Debug-only) | 0.1.0-preview.7.26230.1 |
| `Markdig` (markdown) | 0.44.0 |
| `Microsoft.AspNetCore.Components.QuickGrid` | 10.0.1 |
| `Shiny.Mediator.Maui` / `.Caching.MicrosoftMemoryCache` | 6.1.1 |
| `Sentry.Maui` | 6.4.0 |

## Build patterns adopted from Sherpa
- Native payload bundling: a `Target AfterTargets="Build"` copies `runtimes/{rid}/native/*` into `$(OutDir)$(ApplicationTitle).app/Contents/MonoBundle/runtimes/{rid}/native/` (the macOS build does not do this automatically). Generalize for the FL dylib chain.
- Hardened runtime: `EnableHardenedRuntime=true` + `CodesignEntitlements` (Debug + Release plists). Debug entitlements grant `network.server` (our server feature) and `allow-jit`; **add `com.apple.security.cs.disable-library-validation`** if M0b shows the `dlopen`'d FL dylibs need it.
- Packages resolve from public nuget.org (Sherpa has no repo-root NuGet.config); net11 AppKit build may come from LocalNuGets / staged CI packages via `macos-maui-dogfood`.
