# Implementation Plan: M1 — App Shell + Foundry Local Service Layer + DI + Test/CI Seam

**Branch**: `002-m1-app-shell-foundation` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-m1-app-shell-foundation/spec.md`

## Summary

M1 replaces the throwaway M0 spikes (`spikes/m0a-baseline-app`, `spikes/m0b-fl-console`,
`spikes/m0d-vertical-slice`) with the **real, non-throwaway FoundryForge solution** and the
Foundry Local service layer that M2–M5 build on. It ships **no end-user "wow" feature**; its
value is a correct, tested, DI-wired foundation.

Technical approach, grounded in the proven M0 work (DEC-004/005, M0a–M0d gates, the
`spikes/m0d-vertical-slice` reference):

- Scaffold a clean multi-project solution — an FL-free **`FoundryForge.Core`** (interfaces,
  DTO records, pure-logic seams, the model-state concurrency-gate primitive), an FL-bound
  **`FoundryForge.Foundry`** (the singleton lifecycle manager wrapper, ready-gate, catalog
  service, in-process `IChatClient` adapter, post-v1 stubs), the **`FoundryForge.App`** AppKit
  head (`net10.0-macos`, `Microsoft.NET.Sdk.Razor`, Blazor Hybrid UI), and an
  **`FoundryForge.Tests`** xUnit project that references **only Core** so its pure-logic seam
  tests need no native dylib.
- Promote the M0d `FoundryReadyService` (`Lazy<Task<FoundryLocalManager>>` + `Task.Run`,
  no `.Result`/`.Wait()`) into the real ready-gate every consumer awaits, behind an app-level
  "initializing" route that blocks the chat surface (KI-005, FR-004/005/006).
- Implement the **singleton load/unload concurrency gate** that drains-or-rejects in-flight
  generations before mutating model state (DEC-004 "Singleton concurrency contract", PLAN.md
  line 75; FR-008/009/010) — in M1, not deferred to M5.
- Define `IFoundryCatalogService`, `IChatService` (via the in-process `IChatClient` adapter,
  no loopback socket), and `IEmbeddingService`/`ITranscriptionService`/`ILocalServerService`
  with non-faking stubs (FR-011/012/013).
- Add a user-editable, auditable settings store (cache dir, default model, theme) via
  Preferences + JSON that is never wiped without consent (FR-014/015).
- Stand up the xUnit project + one clean-checkout CI build gated on the pinned versions
  (FR-016/017), and start the real app from proper MAUI Blazor template `_Imports` (KI-006,
  FR-002).

Baseline toolchain is the M0-proven `net10.0-macos` set; the **net11 pin is the separate open
chore T004** and is explicitly out of M1 scope (Assumptions; FR-019) — noted below as a
prerequisite/risk, not a blocker.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0` for libraries/tests; `net10.0-macos`,
`SupportedOSPlatformVersion=14.0`, for the AppKit head). Proven baseline per M0a; net11 is the
separate T004 chore.

**Primary Dependencies** (pinned, central — `Directory.Packages.props`, source of truth
`KNOWN-GOOD-VERSIONS.md`):
- maui-labs AppKit head: `Microsoft.Maui.Platforms.MacOS{,.BlazorWebView,.Essentials}`
  `0.1.0-preview.8.26256.5`; `Microsoft.Maui.Controls` `10.0.41`;
  `Microsoft.AspNetCore.Components.WebView.Maui` `10.0.1`.
- Foundry Local (`sdk` line, **not** `sdk_v2`): `Microsoft.AI.Foundry.Local` `1.2.3`
  (transitive `.Core` 1.2.3 + `Microsoft.ML.OnnxRuntime.Foundry` 1.26.0 +
  `Microsoft.ML.OnnxRuntimeGenAI.Foundry` 0.14.1). Confirmed API surface (M0b/M0d):
  `FoundryLocalManager.CreateAsync(Configuration, ILogger)` / `Instance` / `IsInitialized`;
  `DiscoverEps` / `DownloadAndRegisterEpsAsync`; `GetCatalogAsync → ICatalog`
  (`ListModelsAsync` / `GetModelAsync`); `IModel.IsCachedAsync` / `DownloadAsync` /
  `IsLoadedAsync` / `LoadAsync` / `UnloadAsync` / `GetChatClientAsync`;
  `StartWebServiceAsync` / `StopWebServiceAsync` / `Urls`.
- Microsoft.Extensions.AI `10.0.1` (`IChatClient` adapter surface).
- DevFlow (Debug-only): `Microsoft.Maui.DevFlow.{Agent,Blazor}` `0.1.0-preview.8.26256.5`.

**Storage**: App settings as human-auditable, user-editable JSON via MAUI Essentials
`Preferences` + a JSON document on disk (cache directory, default model, theme). The
multi-GB Foundry Local model cache is protected user data referenced by the cache-directory
setting; never wiped/overwritten without explicit per-action consent.

**Testing**: xUnit (`FoundryForge.Tests`) over the pure-logic seams in `FoundryForge.Core`
(settings model + serialization/merge-with-defaults, catalog filtering predicates, RAM-fit
heuristic, concurrency-gate primitive). No native Foundry Local dylib required — the test
project references **only** the FL-free Core project, and the native-bundling MSBuild target
runs **only** for the `-macos` head.

**Target Platform**: macOS / Apple Silicon only (AppKit head). Models are ONNX-only.

**Project Type**: Desktop application (native macOS AppKit head + Blazor Hybrid UI, in-process;
no server circuit) plus shared libraries.

**Performance Goals**: No UI freeze or deadlock during Foundry Local initialization
(KI-005 / SC-003); ready-gate offloads native init off the BlazorWebView dispatcher; zero
synchronous blocking on the init task anywhere (SC-003). No end-user latency targets in M1
(foundation milestone).

**Constraints**:
- Exactly one `FoundryLocalManager` singleton backs both the in-process UI path and the future
  exposed server; no second manager is ever constructed (Constitution V; FR-003).
- Init runs off the dispatcher (`Task.Run`), UI updates marshalled via
  `InvokeAsync(StateHasChanged)`; **hard rule: no `.Result`/`.Wait()`** on the init task
  (KI-005; FR-005/006).
- Load/unload never proceeds during an active stream on the same model; concurrent mutations
  serialized (FR-008/009).
- In-process `IChatClient` adapter only — no loopback HTTP socket for our own chat (FR-012).
- Capability honesty: no UI/toggle for unsupported FL features; structured output is
  best-effort-only, never "guaranteed JSON" (M0d finding; FR-018).
- `_Imports.razor` includes the full MAUI Blazor template using-set (Components, Web, Forms,
  Routing, JSInterop) or `@onclick`/`@bind` silently break (KI-006; FR-002).
- Pinned versions are not floated; CI fails if a dependency resolves off the pinned set
  (FR-017).

**Scale/Scope**: M1 foundation only — lifecycle/ready-gate, concurrency gate, catalog service,
chat adapter, post-v1 stubs, settings store, test project + one CI job. Excludes all UI
milestones (M2 catalog, M3 management, M4 chat, M5 server toggle) and all post-v1 work
(FR-019). One small solution (~4 projects), no end-user screens beyond the initializing/ready
shell.

**Prerequisite risk (non-blocking)**: The net11 toolchain pin (chore **T004**) is open. M1
builds on the proven `net10.0-macos` baseline and does not block on it; if/when T004 lands, the
pinned set advances under the normal "re-run the M0 gate" rule (Constitution V).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

Evaluated against FoundryForge Constitution v1.0.0 (all five principles). **Result: PASS** on
all five, both pre-Phase-0 and post-Phase-1. No deviations; Complexity Tracking is empty.

| Principle | Assessment | Verdict |
|---|---|---|
| **I. Citation Before Action** | Every architectural claim in this plan and its artifacts cites a source: M0 spec/research/data-model, `spikes/m0d-vertical-slice/*`, DEC-004/005/006/007, KI-001…KI-006, `KNOWN-GOOD-VERSIONS.md`, `Directory.Packages.props`, `build/BundleFoundryLocalNative.targets`, `Entitlements.Debug.plist`, and `docs/PLAN.md` (lines 73–77, 90–96). No invented confidence. | **PASS** |
| **II. Pre-Completion Verification (NON-NEGOTIABLE)** | M1 closes with a real Apple-Silicon DevFlow end-to-end check (app launches, reaches ready without deadlock) + a service-level smoke of the concurrency gate + xUnit green + CI clean on pinned versions, and a `Verified:` line (FR-021/SC-011; quickstart.md). Build success is treated as a prerequisite, not verification. | **PASS** |
| **III. Surgical Changes & Reviewer Independence** | Scope is bounded to the M1 foundation (FR-019); no opportunistic refactoring — the M0 spikes are **archived/deleted** as a clean scaffold replacement (their disposition documented), not refactored in place. Reviewer independence is mandatory: the author is not the sole approver (FR-022/SC-011). | **PASS** |
| **IV. Data Preservation & Capability Honesty** | Settings and the multi-GB model cache are protected user data — never wiped/overwritten without explicit per-action consent; missing/corrupt settings recover non-destructively to documented defaults (FR-014/015; SC-007). No UI/toggle for unsupported FL capabilities; structured output is best-effort-only with no "guaranteed JSON" (FR-018; SC-010). | **PASS** |
| **V. Native-Load & In-Process Discipline** | M0 native-load gate is proven (M0b/M0d, DEC-005). Exactly one `FoundryLocalManager` singleton backs both surfaces, guarded by the load/unload concurrency gate (FR-003/008/009). Chat runs through an in-process `IChatClient` adapter, no loopback socket (FR-012). Pinned known-good set is reused as-is (`Directory.Packages.props`, `build/BundleFoundryLocalNative.targets`, `Entitlements.Debug.plist`); upgrades (net11/T004) re-run the M0 gate. | **PASS** |

**Quality gates (Scope & Quality Gates section):**
- WCAG AA: no end-user screens in M1 beyond the initializing/ready shell; accessibility lands
  with the UI milestones (M2+). The shell state text uses semantic markup and status roles.
- Persistent writes auditable/user-editable & never wiped without consent: satisfied
  (FR-014/015).
- Milestone closes with real Apple-Silicon DevFlow + `Verified:` line: planned (FR-021).
- Telemetry (OpenTelemetry GenAI conventions, no PII): the `IChatClient` adapter is shaped so
  `UseOpenTelemetry()` middleware can compose around it (PLAN.md line 61); full telemetry
  wiring is M4, but the seam is reserved here with no PII in logs.
- Every workaround links a `KNOWN-ISSUES.md` tracking entry: KI-005 and KI-006 are codified
  into the M1 service layer and app scaffold respectively; any new M1 workaround gets an entry
  (FR-020/SC-012).

## Project Structure

### Documentation (this feature)

```text
specs/002-m1-app-shell-foundation/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output — service interface contracts
│   ├── IFoundryLifecycle.md
│   ├── IModelStateGate.md
│   ├── IFoundryCatalogService.md
│   ├── IChatService.md
│   ├── ISettingsService.md
│   └── IPostV1Services.md
├── checklists/
│   └── requirements.md  # Spec quality checklist (already present)
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FoundryForge.Core/                 # net10.0 — NO Foundry Local SDK reference
│   ├── FoundryForge.Core.csproj       #   (keeps the pure-logic test seam dylib-free)
│   ├── Abstractions/                   # interfaces every consumer/test codes against
│   │   ├── IFoundryLifecycle.cs        #   ready-gate contract (ReadyAsync / GetManager)
│   │   ├── IModelStateGate.cs          #   load/unload concurrency-gate contract
│   │   ├── IFoundryCatalogService.cs
│   │   ├── IChatService.cs
│   │   ├── ISettingsService.cs
│   │   ├── IEmbeddingService.cs        # post-v1 (stubbed impl lives in Foundry)
│   │   ├── ITranscriptionService.cs    # post-v1
│   │   └── ILocalServerService.cs      # post-v1
│   ├── Models/                         # DTO records (FL-free): ModelInfo, ModelVariant,
│   │   │                               #   AppSettings, CatalogFilter, RamFitResult …
│   ├── Concurrency/
│   │   └── ModelStateGate.cs           # pure SemaphoreSlim + active-stream tracking primitive
│   ├── Settings/
│   │   └── SettingsDocument.cs         # JSON shape + merge-with-defaults + consent-guard logic
│   └── Catalog/
│       ├── CatalogFilter.cs            # pure filtering predicates (device/task/provider)
│       └── RamFitHeuristic.cs          # pure "model size vs free RAM + margin" logic
│
├── FoundryForge.Foundry/              # net10.0 — references Core + FL SDK + MEAI
│   ├── FoundryForge.Foundry.csproj
│   ├── FoundryLifecycle.cs             # singleton manager wrapper + ReadyAsync (Task.Run,
│   │                                   #   Lazy<Task<FoundryLocalManager>>, IAsyncDisposable)
│   ├── FoundryCatalogService.cs        # wraps ICatalog/IModel; load/unload via IModelStateGate
│   ├── FoundryChatClient.cs            # in-process IChatClient adapter (no socket)
│   ├── ChatService.cs                  # IChatService over the adapter (middleware-composable)
│   ├── PreferencesSettingsService.cs   # ISettingsService: Preferences + JSON persistence
│   └── PostV1/                         # non-faking stubs: throw NotSupportedException("v1")
│       ├── StubEmbeddingService.cs
│       ├── StubTranscriptionService.cs
│       └── StubLocalServerService.cs
│
└── FoundryForge.App/                  # net10.0-macos — Microsoft.NET.Sdk.Razor, AppKit head
    ├── FoundryForge.App.csproj        # references Core + Foundry; imports the bundle target
    ├── Program.cs                      # NSApplication bootstrap (from M0d)
    ├── App.cs                          # MAUI Application + BlazorHostPage window
    ├── BlazorHostPage.cs
    ├── MauiProgram.cs                  # DI registration (singletons), DevFlow (Debug)
    ├── AppDelegate.cs
    ├── _Imports.razor                  # FULL template using-set (KI-006)
    ├── Components/
    │   ├── App.razor / Routes.razor
    │   └── Pages/
    │       ├── Initializing.razor      # app-level "initializing" guard (blocks chat surface)
    │       └── Ready.razor             # minimal ready shell (no M2+ UI)
    ├── wwwroot/                        # index.html, app.css (BundleResource)
    ├── Info.plist
    └── Platforms/macOS/                # Entitlements reference (../../Entitlements.Debug.plist)

tests/
└── FoundryForge.Tests/               # net10.0 — references Core ONLY (no FL dylib)
    ├── FoundryForge.Tests.csproj
    ├── SettingsDocumentTests.cs        # defaults, round-trip, never-wipe-without-consent
    ├── CatalogFilterTests.cs           # filtering predicates
    ├── RamFitHeuristicTests.cs         # size-vs-free-RAM-with-margin
    └── ModelStateGateTests.cs          # drains/rejects, serialization, per-model isolation

# Reused as-is from M0 (repository root):
Directory.Packages.props                # central pinned versions (source: KNOWN-GOOD-VERSIONS.md)
build/BundleFoundryLocalNative.targets  # imported by FoundryForge.App.csproj only
Entitlements.Debug.plist                # referenced by FoundryForge.App (CodesignEntitlements)
FoundryForge.sln                       # new in M1 — ties the four projects together
.github/workflows/ci.yml                # new in M1 — clean-checkout restore+build+test on pins
```

**Structure Decision**: A four-project solution. The split of **`FoundryForge.Core`** (no FL
SDK reference) from **`FoundryForge.Foundry`** (FL-bound) is deliberate and serves a concrete
spec requirement, not gratuitous layering: it guarantees the pure-logic seam tests
(`FoundryForge.Tests` → Core only) run **with no native Foundry Local dylib present**
(FR-016, SC-008, Edge case "Unit tests run with no native dylib present"). The native-bundling
MSBuild target (`build/BundleFoundryLocalNative.targets`) is imported **only** by the
`-macos` head, so neither Core nor Tests ever bundles or loads native payload. `FoundryForge.App`
is the AppKit host carrying the Razor UI (Blazor Hybrid in-process, per DEC-004), reusing the
M0-proven `Directory.Packages.props`, bundle target, and `Entitlements.Debug.plist` unchanged.
This project count is **not** a Constitution violation (no Complexity Tracking entry required);
it is the simplest shape that satisfies both FR-001 (host + Razor UI + shared services/models)
and the dylib-free test seam. The M0 spikes (`spikes/m0a-baseline-app`, `spikes/m0b-fl-console`,
`spikes/m0d-vertical-slice`) are **archived/deleted** once M1 scaffolds the real solution — they
were declared throwaway in their own specs and DEC-006/007; their proven patterns are carried
forward into the real projects above, not refactored in place (Constitution III).

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _(none)_  | _(n/a)_    | _(n/a)_                              |
