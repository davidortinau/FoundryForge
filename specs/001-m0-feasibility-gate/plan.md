# Implementation Plan: M0 Feasibility Gate

**Branch**: `001-m0-feasibility-gate` | **Date**: 2026-06-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-m0-feasibility-gate/spec.md`

## Summary

M0 is the linchpin go/no-go gate: prove, on a real Apple Silicon Mac, that a Foundry Local-powered Blazor Hybrid UI hosted on the maui-labs AppKit macOS head can build, bundle and load the Foundry Local native dylib chain in-process, render UI, and stream a real model reply. The work is four sequential sub-gates (M0a → M0b → M0c → M0d), each a cheap, independently demonstrable proof that fails before the next begins. The deliverable is **a decision and its evidence**, produced via disposable spike heads, not product code. Approach is lifted directly from the validated MAUI.Sherpa pattern (native-payload MSBuild copy target + hardened-runtime entitlements), adapted for Foundry Local's multi-dylib `@rpath` chain.

## Technical Context

**Language/Version**: C# on `net11.0-macos` (preview, primary track); `net10.0-macos` Sherpa reference set as documented fallback (M0a decides).

**Primary Dependencies**: maui-labs AppKit `Microsoft.Maui.Platforms.MacOS{,.BlazorWebView,.Essentials}`; `Microsoft.Maui.Controls` (11.x preview, matched); `Microsoft.AspNetCore.Components.WebView.Maui`; `Microsoft.AI.Foundry.Local` (`sdk` vs `sdk_v2` confirmed in M0b); `Microsoft.Maui.DevFlow.{Agent,Blazor}` (Debug-only). Versions pinned via `Directory.Packages.props` + `KNOWN-GOOD-VERSIONS.md`, staged through the `macos-maui-dogfood` skill.

**Storage**: Foundry Local-managed model cache (multi-GB user data; downloaded, never wiped). No application database in M0.

**Testing**: Per-gate manual end-to-end on real Apple Silicon hardware via MAUI DevFlow; bundle verification via `find *.app -name '*.dylib'` + `otool -L`; MSBuild copy verification via `mcp-binlog-tool` on the build binlog. (The xUnit pure-logic seam is an M1 deliverable, not M0.)

**Target Platform**: macOS 14+ on Apple Silicon (arm64) only. No iOS / Android / Mac Catalyst.

**Project Type**: Desktop app (macOS AppKit host + Blazor Hybrid). M0 produces three disposable spike heads, not the shipping app.

**Performance Goals**: Not a performance gate. Success = the dylib chain loads and one reply streams incrementally without a native crash or library-validation failure. No latency/throughput target.

**Constraints**: In-process native load must succeed under hardened runtime + library validation (likely needs `com.apple.security.cs.disable-library-validation` + nested-dylib re-signing); async `CreateAsync()` init with **no `.Result`/`.Wait()`**; first-run model download needs network, offline after; build-locally-only (unsigned dogfood `.app`).

**Scale/Scope**: 3 throwaway spike projects + 1 shared MSBuild target + 1 entitlements plist; one small test model (`qwen2.5-0.5b`); decision/evidence artifacts. No product surface area.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Citation Before Action | Every gate cites a source: Sherpa's `_BundleNativeForMacOS` target, `otool -L` linkage output, binlog evidence, FL SDK source. M0b reads dylib/MSBuild source before iterating on the slow rebuild loop. | PASS |
| II. Pre-Completion Verification (NON-NEGOTIABLE) | No gate passes on build success alone; each closes with a real Apple-Silicon DevFlow end-to-end check and a `Verified:` line (FR-012, FR-006…FR-010). This feature *is* the verification discipline applied to the foundation. | PASS |
| III. Surgical Changes & Reviewer Independence | Spike heads are minimal and disposable; each changed line traces to a gate's proof. Reviewer independence enforced through Squad handoffs; the author of a gate spike does not self-certify its go/no-go. | PASS |
| IV. Data Preservation & Capability Honesty | Model cache is downloaded, never wiped. M0d records server `tools`/`response_format` support honestly as a scope-bounding input; no fake UI or dead toggle (FR-011, FR-014). | PASS |
| V. Native-Load & In-Process Discipline | M0 directly enforces this principle: it proves the native dylib chain bundles/signs/loads before any feature is built, uses the in-process path, and assumes one `FoundryLocalManager`. M0b is the principle's proof obligation. | PASS |

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/001-m0-feasibility-gate/
├── plan.md              # This file (/speckit.plan output)
├── research.md          # Phase 0 output — how each gate is run, unknowns resolved
├── data-model.md        # Phase 1 output — gate/evidence/decision record model
├── quickstart.md        # Phase 1 output — runnable per-gate validation guide
├── contracts/           # Phase 1 output — per-gate exit-criteria contracts
│   ├── gate-m0a-exit.md
│   ├── gate-m0b-exit.md
│   ├── gate-m0c-exit.md
│   └── gate-m0d-exit.md
├── checklists/
│   └── requirements.md  # Spec quality checklist (already passing)
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

M0 produces disposable spike heads kept out of the eventual product tree. They live under `spikes/` and are deleted (or archived) once M0d is green and M1 scaffolds the real solution.

```text
spikes/                                  # throwaway M0 proof heads — NOT product code
├── m0a-baseline-app/                    # empty AppKit + BlazorWebView app (builds + launches)
│   ├── Directory.Packages.props         # the pinned version set under test
│   └── global.json / NuGet.config       # staged via macos-maui-dogfood
├── m0b-fl-console/                      # console head: FL native-load spike (no MAUI/Blazor)
└── m0d-vertical-slice/                  # AppKit + Blazor slice (catalog → load → stream)
                                         # M0c is exercised inside the m0a/m0d app, not separate

build/
└── BundleFoundryLocalNative.targets     # MSBuild copy target adapted from Sherpa's _BundleNativeForMacOS

Entitlements.Debug.plist                 # hardened-runtime entitlements (+ disable-library-validation if M0b needs it)
```

**Structure Decision**: Disposable `spikes/` heads, one per gate that needs code (M0a baseline app, M0b console head, M0d vertical slice; M0c rides inside the existing apps). Shared MSBuild target and entitlements live at repo root so M1 can promote the proven ones into the real solution. No product `src/` is created in M0 — FR-002 forbids application code until M0d passes. Pinned versions are recorded in `KNOWN-GOOD-VERSIONS.md` and `Directory.Packages.props`.

## Complexity Tracking

> No constitution violations. Section intentionally empty.
