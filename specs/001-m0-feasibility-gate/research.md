# Phase 0 Research: M0 Feasibility Gate

This gate is itself research, so Phase 0 captures **how each sub-gate is executed**, what is already known (cited), and what M0 must determine empirically. Each entry follows: Decision / Rationale / Alternatives considered.

## R1 — net11 vs net10 toolchain (resolves M0a unknown)

- **Decision**: Primary track is `net11.0-macos` with the net11-compatible maui-labs AppKit build, staged via the `macos-maui-dogfood` skill and pinned in `Directory.Packages.props` + `KNOWN-GOOD-VERSIONS.md`. If the net11 AppKit set is unstable, fall back to the proven Sherpa net10 set (AppKit `0.1.0-preview.8.26256.5`, `Microsoft.Maui.Controls 10.0.41`, `Microsoft.AspNetCore.Components.WebView.Maui 10.0.1`) and record the decision.
- **Rationale**: net11 is David's active dogfood track and the project's stated target. Sherpa proves the *pattern* works on net10, giving a guaranteed-good fallback so M0a cannot dead-end the project. Pinning defends the multi-preview stack against silent churn.
- **Alternatives considered**: Start on net10 for safety — rejected because v1 ships on net11 and we want the real toolchain proven first; net10 stays as the safety net, not the default.

## R2 — Foundry Local package identity (`sdk` vs `sdk_v2`) (resolves M0b unknown)

- **Decision**: T011 research confirms the current public/documented Foundry Local .NET package identity is `Microsoft.AI.Foundry.Local` (`sdk`), with proposed M0b pin `1.2.3`. The `sdk_v2` runtime package shape is not the line to pin for M0b because `Microsoft.AI.Foundry.Local.Runtime` was not published on NuGet as of T011 (flat-container 404). Keep the proposed pins clearly labeled until hardware confirmation in M0b.
- **Rationale**: Bishop's T011 inbox note (`.squad/decisions/inbox/bishop-fl-package-identity.md`) cites Microsoft Learn, the `microsoft/Foundry-Local` repo, and NuGet flat-container metadata showing `Microsoft.AI.Foundry.Local` 1.2.3 exists, depends on `Microsoft.AI.Foundry.Local.Core` 1.2.3, and is ahead of the GitHub release notes (v1.2.1). Pin NuGet and record the release-note mismatch.
- **Alternatives considered**: Pin `sdk_v2` / `Microsoft.AI.Foundry.Local.Runtime` — rejected for M0b because T011 found the source shape but no published NuGet runtime package. Pin the GitHub release-note version — rejected because NuGet has the newer stable 1.2.3 package.

## R3 — Native dylib chain bundling (the M0b core)

- **Decision**: Adapt Sherpa's `_BundleNativeForMacOS` MSBuild target (`Target AfterTargets="Build"` copying `runtimes/{rid}/native/*` into `Contents/MonoBundle/runtimes/{rid}/native/`) into a shared `build/BundleFoundryLocalNative.targets`, generalized to copy the confirmed public-sdk osx-arm64 chain: `Microsoft.AI.Foundry.Local.Core.dylib` + `libonnxruntime.dylib` + `libonnxruntime-genai.dylib`. Keep copying `runtimes/osx-arm64/native/*` so any restore-time WebGPU EP/plugin payloads are preserved. Verify with `find *.app -name '*.dylib'` (chain present) and `otool -L` on each (inter-dylib `@rpath` resolves). Use `mcp-binlog-tool` on the build binlog to confirm exactly what copies.
- **Rationale**: T011 research (`.squad/decisions/inbox/bishop-fl-package-identity.md`) corrected the plan assumption: the current public sdk line ships `Microsoft.AI.Foundry.Local.Core.dylib`, ONNX Runtime, and ONNX Runtime GenAI; it does not ship `libfoundry_local.dylib` or a separate Dawn dylib in the NuGet closure. WebGPU/Metal appears as plugin/on-demand behavior, so M0b still confirms exact restore-time filenames on hardware.
- **Alternatives considered**: `NativeLibrary.SetDllImportResolver` redirect — kept as a workaround-of-last-resort if the copy target can't satisfy `@rpath`; not the primary path because bundling is the distributable-shaped solution. Trusting default tooling to copy the chain — rejected (PLAN.md: "Do not assume it's automatic"). Bundling assumed `libfoundry_local`/Dawn names — rejected after T011 NuGet/source evidence corrected the actual public-sdk payload.

## R4 — Library validation under hardened runtime (the M0b risk)

- **Decision**: Run with `EnableHardenedRuntime=true` and a Debug entitlements plist that already grants `network.server` and `cs.allow-jit`. If the `dlopen`'d FL dylibs fail library validation, add `com.apple.security.cs.disable-library-validation` and re-sign nested dylibs; record the required entitlement. If load still fails, declare M0b no-go with `otool`/crash evidence.
- **Rationale**: Sherpa's CLI is `exec`'d (no library-validation hit); FL is `dlopen`'d into our process, which will hit validation. The entitlement + re-sign remedy is the known mitigation. This is the project's true go/no-go, so a no-go here is a valid, evidence-backed outcome.
- **Alternatives considered**: Disabling the sandbox entirely — unnecessary; Sherpa already runs `app-sandbox=false` in Debug and the targeted entitlement is narrower. Signing with a Developer ID now — out of scope (M7); Debug ad-hoc signing suffices for build-locally-only dogfood.

## R5 — BlazorWebView capability probe scope (M0c)

- **Decision**: Limit M0c to the two residual unknowns: (1) local file intake into `<input type=file>` (WKWebView sandboxes file access), and (2) Hot Reload of UI edits. Exercise both inside the M0a/M0d app. Record findings; if file intake is blocked, flag the post-v1 native file-intake shim. M0c does **not** block M0 (informational gate).
- **Rationale**: Sherpa already proves rich Blazor Hybrid UI (Markdig, QuickGrid) renders on the AppKit head, so core UI viability is confirmed. Only file intake and the inner-loop ergonomics remain genuinely unknown.
- **Alternatives considered**: A full UI capability matrix — rejected as over-scoped; the renderer is proven, only the two seams matter for v1/post-v1 planning.

## R6 — In-process chat path + server capability check (M0d)

- **Decision**: M0d uses the **in-process** path: `CreateAsync` → `ICatalog` list → download+load `qwen2.5-0.5b` → stream one reply into a Razor component, wrapping the SDK in a thin `IChatClient` adapter so the slice mirrors the real M1 architecture. Separately, start the exposed local server and send a tool-calling request and a `response_format` (structured-output) request via external `curl`; record whether each is honored as the go/no-go for v1 tool-calling and structured output.
- **Rationale**: The in-process adapter removes the loopback-HTTP failure surface for our own chat (PLAN.md), and the slice should prove the real architecture, not a throwaway shape. The server check is the only place tool/structured-output support can be confirmed before committing M4 scope.
- **Alternatives considered**: Route the slice's chat through `127.0.0.1` — rejected; that reintroduces port binding + SSE chunk-boundary risk the architecture explicitly avoids. Defer the server capability check to M4 — rejected; descoping decisions must be made before planning M4.

## R7 — Verification + decision recording discipline

- **Decision**: Each gate closes with a real Apple-Silicon DevFlow end-to-end check (not build success) and a `Verified:` line; every go/no-go, version-set, and scope decision is appended to `.squad/decisions.md`; any upstream workaround is logged in `KNOWN-ISSUES.md` with a tracking link (route FL issues to Maanav Dalal / Meng Tang).
- **Rationale**: Constitution Principle II + FR-012/FR-013/FR-015. M0's value is the decision and its audit trail; undocumented passes are not passes.
- **Alternatives considered**: Lightweight ad-hoc notes — rejected; the decision log is the canonical, reviewable record the rest of the project depends on.

## Open items intentionally deferred to execution

These are resolved *by running* the gates, not by pre-research (that is the point of M0):

- Exact net11 AppKit build number that builds + launches (M0a output → `KNOWN-GOOD-VERSIONS.md`).
- Whether `disable-library-validation` is actually required, and the exact nested-dylib re-sign step (M0b output → `Entitlements.Debug.plist`).
- File-intake support and Hot Reload behavior on the AppKit WKWebView (M0c output).
- Whether the FL local server honors `tools` and `response_format` (M0d output → M4 scope decision).
