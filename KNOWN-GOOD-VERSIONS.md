# KNOWN-GOOD-VERSIONS.md

Frozen, known-good dependency set. Do **not** float to "latest." Any bump is a deliberate, isolated chore that re-runs the M0 gate (see `docs/PLAN.md`).

## Target toolchain (net11 — PROVEN; net11-primary track)
Validated end-to-end on Apple Silicon (empty app + full FL slice: build, launch, FL native load under net11 CoreCLR in the signed .app, catalog/load/stream). Reference: SentenceStudio (David's working net11.0-macos app). maui-labs AppKit + DevFlow resolve from LocalNuGets; MAUI 11 previews from the dnceng dotnet10 feed (see `NuGet.config`). No new SDK installed — uses the installed Preview 5.

| Component | net11 version | Source | Status |
|---|---|---|---|
| .NET SDK | 11.0.100-preview.5.26302.115 (global.json: rollForward latestPatch, allowPrerelease) | installed | **PROVEN** |
| TFM | net11.0-macos, SupportedOSPlatformVersion 14.0 | — | **PROVEN** |
| maui-labs AppKit `Microsoft.Maui.Platforms.MacOS{,.BlazorWebView,.Essentials}` | 0.26.0-dev | LocalNuGets | **PROVEN** |
| `Microsoft.Maui.Controls` | 11.0.0-preview.4.26230.3 | dotnet10 feed | **PROVEN** |
| `Microsoft.AspNetCore.Components.WebView.Maui` | 11.0.0-preview.4.26230.3 | dotnet10 feed | **PROVEN** |
| `Microsoft.Maui.DevFlow.{Agent,Blazor}` (Debug-only) | 0.25.0-dev | LocalNuGets | **PROVEN** |
| `Microsoft.AI.Foundry.Local` | **1.2.3** (`sdk` line; stable) — pulls `.Core` 1.2.3 + ORT.Foundry 1.26.0 + ORT GenAI.Foundry 0.14.1 transitively | nuget.org | **CONFIRMED** (loaded + inference under net11) |
| `Microsoft.Maui.Essentials.AI` (Apple Intelligence `IChatClient`) | 11.0.0-preview.2.26152.10 | dotnet10 feed | **PROVEN** (Phase 0 spike: macOS 26.5.1 / M4 Max, ~1.1s JSON extraction). Needs `<NoWarn>MAUIAI0001</NoWarn>` (experimental) + runtime guard `OperatingSystem.IsMacOSVersionAtLeast(26)`; Foundation Models async resumes on the main run loop. |

## Proven reference baseline — MAUI.Sherpa (net10, known to work)
Use as the fallback reference and to validate the pattern. Source: [Redth/MAUI.Sherpa](https://github.com/Redth/MAUI.Sherpa) `src/MauiSherpa.MacOS`.

| Component | Version |
|---|---|
| .NET SDK (`global.json`) | 10.0.301 (rollForward latestMajor, allowPrerelease) |
| TargetFramework | net10.0-macos, SupportedOSPlatformVersion 14.0 |
| `Microsoft.Maui.Platforms.MacOS{,.BlazorWebView,.Essentials}` | 0.1.0-preview.8.26256.5 |
| `Microsoft.Maui.Controls` | 10.0.41 |
| `Microsoft.AspNetCore.Components.WebView.Maui` | 10.0.1 |
| `Microsoft.Maui.DevFlow.Agent` / `.Blazor` (Debug-only) | 0.1.0-preview.8.26256.5 (preview.7.26230.1 is NOT on the feed — corrected in M0a) |
| `Markdig` (markdown) | 0.44.0 |
| `Microsoft.AspNetCore.Components.QuickGrid` | 10.0.1 |
| `Shiny.Mediator.Maui` / `.Caching.MicrosoftMemoryCache` | 6.1.1 |
| `Sentry.Maui` | 6.4.0 |

## Build patterns adopted from Sherpa
- Native payload bundling: a `Target AfterTargets="Build"` copies `runtimes/{rid}/native/*` into `$(OutDir)$(ApplicationTitle).app/Contents/MonoBundle/runtimes/{rid}/native/` (the macOS build does not do this automatically). Generalize for the FL dylib chain.
- Hardened runtime: `EnableHardenedRuntime=true` + `CodesignEntitlements` (Debug + Release plists). Debug entitlements grant `network.server` (our server feature) and `allow-jit`; **add `com.apple.security.cs.disable-library-validation`** if M0b shows the `dlopen`'d FL dylibs need it.
- Packages resolve from public nuget.org (Sherpa has no repo-root NuGet.config); net11 AppKit build may come from LocalNuGets / staged CI packages via `macos-maui-dogfood`.
