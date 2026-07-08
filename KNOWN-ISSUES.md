# KNOWN-ISSUES.md

Running log of workarounds for upstream gaps/bugs. **Every workaround in the codebase has an entry here**, links its tracking issue, and is removed when the upstream fix lands. Policy: never block — workaround, file upstream, remove on fix (see `AGENTS.md` and `docs/PLAN.md`).

Ownership routing:
- **David-owned** (maui-labs AppKit, Comet, DevFlow, maui CLI, Maui.Essentials.AI): fix directly in `~/work/maui-labs`, dogfood via `~/work/LocalNuGets/`.
- **Foundry Local** (`microsoft/Foundry-Local`): local workaround + file issue, route to Maanav Dalal (IC) / Meng Tang (portfolio).
- **Other** (dotnet/maui, dotnet/macios, MEAI, etc.): shim + pin known-good + file upstream.

## Template

```
### KI-NNN — <short title>
- Area: <FL SDK | maui-labs AppKit | dotnet/macios | BlazorWebView | other>
- Symptom: <what breaks>
- Workaround: <what we did, where in the code>
- Upstream: <issue URL or "not yet filed">
- Remove when: <condition>
- Status: <open | upstream-fixed-pending-removal | closed>
```

## Open issues

### KI-001 — DevFlow native screenshot does not capture the WKWebView layer on the AppKit head
- Area: maui-labs AppKit / DevFlow (David-owned)
- Symptom: `maui devflow ui screenshot` on `net*-macos` (AppKit) captures the native window chrome only; the BlazorWebView (WKWebView) content region comes back blank/white even though the page is fully rendered. CDP `maui devflow webview Page captureScreenshot` returns `"unimplemented"`. Confirmed in M0a: DOM (`webview source` / `Runtime evaluate`) showed the live `<h1>` with computed black-on-white 44px text on-screen, and the user visually confirmed the heading — yet both screenshot paths showed/produced no WebView content.
- Workaround: verify Blazor-on-AppKit visual state via DOM inspection (`maui devflow webview source`, `Runtime evaluate "document.body.innerHTML"`, computed-style queries) plus a human eyeball; do not rely on `ui screenshot` for WebView content on AppKit.
- Upstream: not yet filed (David-owned — fix in maui-labs DevFlow: implement WKWebView-layer capture / CDP `Page.captureScreenshot` for the macOS agent).
- Remove when: DevFlow macOS agent captures the WKWebView layer (native screenshot) or implements CDP `Page.captureScreenshot`.
- Update (M2): root cause refined — the WKWebView GPU-composited layer is only in the window backing store when the window is FRONTMOST. `ui screenshot`, `screencapture -l <windowid>`, AND CDP `webview Page captureScreenshot` (still "unimplemented") all return blank WebView content for a background-launched app. David's interactive sessions capture fine because his app window is frontmost. Autonomous/background verification uses DOM inspection (`webview source`/`Runtime evaluate`) as the sanctioned evidence path (the M2 catalog-ui.dom contract designs for this). To get pixels: bring the window frontmost (focus) then `ui screenshot`.
- Status: open

### KI-002 — DevFlow Agent/Blazor preview.7.26230.1 not on the public feed
- Area: maui-labs DevFlow (David-owned)
- Symptom: pinning `Microsoft.Maui.DevFlow.{Agent,Blazor}` `0.1.0-preview.7.26230.1` (the Sherpa-listed version) fails restore (NU1603); NuGet resolves `0.1.0-preview.8.26256.5` instead.
- Workaround: pinned `0.1.0-preview.8.26256.5` (matches the AppKit preview.8 band) in `Directory.Packages.props` + `KNOWN-GOOD-VERSIONS.md`.
- Upstream: not yet filed (David-owned — either publish preview.7 or update Sherpa's reference).
- Remove when: the intended DevFlow version is published, or the pin is intentionally advanced.
- Status: open

### KI-003 — Foundry Local Core dylib ships a hardcoded CI install-name path
- Area: FL SDK (Foundry Local)
- Symptom: `otool -L Microsoft.AI.Foundry.Local.Core.dylib` shows LC_ID_DYLIB = `/Users/cloudtest/vss/_work/1/s/.../Microsoft.AI.Foundry.Local.Core.dylib` (a build-agent absolute path), not an `@rpath` install name. Benign for the M0b plain console (the .NET P/Invoke resolver loads by filename), so load + inference succeeded.
- Workaround: none needed for console/M0b. **Watch item for M0d**: when the FL chain is bundled into the signed `.app` (`Contents/MonoBundle`) under hardened runtime, this absolute install-name may need `install_name_tool -id @rpath/...` + nested-dylib re-signing. Verify during M0d.
- Upstream: not yet filed (Foundry Local — route to Maanav Dalal / Meng Tang) if M0d confirms it's a problem.
- Remove when: FL Core ships an `@rpath` install name, or M0d proves the absolute path is harmless in the signed bundle.
- Status: open (watch)

### KI-004 — AppKit-head app exits immediately under `open` / `dotnet watch` (persists only when binary run directly)
- Area: maui-labs AppKit (David-owned)
- Symptom: launching `FoundryStudio M0a Baseline.app` via `open App.app` or via `dotnet watch ... run` results in the process exiting immediately (`dotnet watch` logs `[M0aBaselineApp (net10.0-macos)] Exited`); no window persists and no DevFlow agent stays connected. The same build **stays alive and serves a DevFlow agent when the inner Mach-O binary is run directly** (`.app/Contents/MacOS/<exe>` in an attached/foreground process). Consequence: the standard Hot Reload loop (`dotnet watch`) cannot be exercised on the AppKit head in this configuration.
- Workaround: for the DevFlow inspect loop, run the binary directly in an attached/async shell (not `open`); for Hot Reload during M1 dev, use the dedicated tooling (`maui-hot-reload-diagnostics` / hotreload-sentinel) and revisit once the launch-lifecycle is fixed.
- Secondary: `dotnet watch` also logged `Failed to read '...obj\Debug/net10.0-macos/.../staticwebassets.development.json'` (backslash/forward-slash mixed path) and duplicate-Razor-source warnings (spike csproj globbing `Components/**/*.razor` overlapping SDK defaults — cosmetic, spike-only).
- Upstream: not yet filed (David-owned — maui-labs AppKit app lifecycle under `open`/`dotnet watch`; and the staticwebassets path-separator bug).
- Remove when: AppKit-head apps stay alive under `open`/`dotnet watch` and Hot Reload works through `dotnet watch`.
- Status: open

### KI-005 — Blazor async-init must run off the BlazorWebView dispatcher thread
- Area: app architecture (our code) / maui-labs BlazorWebView
- Symptom: awaiting `FoundryLocalManager.CreateAsync` directly from a component `OnInitializedAsync` ran FL's heavy synchronous native-load on the WebView dispatcher thread and starved first render (UI froze mid-render). Fix: kick init off the dispatcher via `Task.Run(InitializeAsync)` behind the `Lazy<Task>` ready-gate, and marshal UI updates with `await InvokeAsync(StateHasChanged)`.
- Workaround: implemented in M0d FoundryReadyService (Task.Run) + Home.razor (InvokeAsync(StateHasChanged)). **M1 design rule:** the singleton FoundryLocalManager init and any blocking FL call must never run on the UI dispatcher; ReadyAsync gate offloads to a background thread; no `.Result`/`.Wait()`.
- Upstream: n/a (our architecture) — bake into M1 service layer.
- Remove when: n/a — this is a permanent design constraint (close once codified in M1 services).
- Status: open (design rule for M1)

### KI-006 — Blazor _Imports.razor must include Microsoft.AspNetCore.Components.Web (or @onclick/@bind silently break)
- Area: app setup (our code)
- Symptom: M0d's `_Imports.razor` omitted `@using Microsoft.AspNetCore.Components.Web`. Build SUCCEEDED with no error, but `@onclick`/`@bind` were not compiled as event handlers — they rendered as literal DOM attributes (`@onclick="..."` in the HTML) and the interactive render tree truncated at the first event-bearing element. m0a didn't catch this (no event handlers). Diagnosed via DevFlow DOM inspection (literal `@onclick` present + render truncation).
- Workaround: added `@using Microsoft.AspNetCore.Components.Web` to _Imports.razor → all buttons render and are interactive.
- Upstream: n/a (our code) — but note: a missing core using producing a SILENT runtime break (no build error) is a sharp edge; the standard MAUI Blazor template includes it. **M1 rule:** start the real app from the proper MAUI Blazor template _Imports (Components, Routing, Web, Forms, JSInterop).
- Remove when: codified in M1 app scaffold.
- Status: open (setup rule for M1)

### KI-007 — Code-review findings to resolve at/before M1 (from /review of the M0 + net11 changeset)
- Area: M1 implementation requirements (our code) — surfaced by independent code review; spikes are throwaway so these are recorded as binding M1 rules, not spike edits.
- Findings:
  1. **[High] Reproducibility/CI:** `NuGet.config` `localnugets` is an absolute machine path (`/Users/davidortinau/work/LocalNuGets`) and the maui-labs/DevFlow pins are mutable `-dev` builds. Accepted for the **build-locally dogfood phase** (v1 is not distributable; AGENTS.md). **M1/CI MUST** switch to a portable source (repo-relative committed feed or published preview) and immutable build-numbered versions before the real `restore+build` CI job replaces the placeholder (`.github/workflows/ci.yml` is currently an echo).
  2. **[Med] Streaming guard:** the FL chat streaming pattern indexes `chunk.Choices[0]` unguarded (FoundryChatClient.cs, SliceCatalogService.cs, m0b Program.cs). OpenAI-style streams emit terminal/usage frames with empty `Choices` → `IndexOutOfRangeException`. **M1 IChatService MUST** guard `if (chunk.Choices is { Count: > 0 })` (+ null-check Message) before indexing.
  3. **[Med] Undeclared transitive dep:** the chat adapter binds directly to `Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage` (a transitive type of Microsoft.AI.Foundry.Local) with no explicit pinned PackageReference. **M1 MUST** either pin Betalgo explicitly or isolate FL message construction behind an internal abstraction so the adapter doesn't couple to an undeclared third-party model.
  4. **[Med] Faulted-init memoization:** `FoundryReadyService` caches the init `Task` via `Lazy<Task>`; a transient init failure is cached permanently (app bricked until restart, no retry). **M1 lifecycle/IModelStateGate MUST** reset the gate on failure and allow re-init.
  5. **[Low] Native re-sign ordering:** `build/BundleFoundryLocalNative.targets` copies the FL dylib chain `AfterTargets="Build"` (after codesign). M0d empirically loaded fine under ad-hoc hardened-runtime signing (see DEC M0d / KI-003), but for M1/M7 the native bundling MUST be folded into the signing pipeline so the `.app` is signed AFTER payloads land (or `disable-library-validation` enabled deliberately). Tie-in: KI-003.
- Remove when: each item is resolved in M1 (or M7 for #5) and verified.
- Status: open (M1 backlog)

### KI-008 — M1 code-review deferrals (FoundryLifecycle dispose race, shutdown only)
- Area: app architecture (our code) — from the M1 /review (reviewer-independent). No Critical/High; the concurrency design (faulted-retry, drain, mutate-then-lease) was confirmed correct.
- Items (both benign — only manifest at app shutdown, M2 hardening):
  1. `FoundryLifecycle.DisposeAsync` disposes the manager only if init `IsCompletedSuccessfully` at the check; an init completing *after* the check (shutdown race) leaves that process-global manager undisposed. Impact nil for M1.
  2. `_disposed` is non-volatile and disposal isn't synchronized with `_initLock`; a concurrent `GetManagerTypedAsync` between its disposed-check and `_initLock.WaitAsync` could see a disposed semaphore at shutdown. Benign.
- Workaround: none needed for M1 (shutdown-only, process-global manager).
- Remove when: M2 hardens the lifecycle dispose path (volatile `_disposed`, dispose under lock, continuation-dispose for late-completing init).
- Status: open (M2 hardening)

### KI-009 — BrowseAsync(CachedOnly) double-filters on info.Cached (M3 watch)
- Area: app/FL integration (our code) — from M2 /review. NOT exercised by the M2 UI (which calls BrowseAsync() with no CachedOnly and filters AllModels in-memory).
- Symptom: BrowseAsync with CachedOnly=true sources from FL GetCachedModelsAsync, then re-applies CatalogFilter.Apply which drops !model.IsCached (mapped from info.Cached). If FL's Info.Cached isn't reliably true for models returned by GetCachedModelsAsync, the cached-only browse could return empty despite cached models existing.
- Workaround: none needed for M2. For M3 (cache management UI): trust the GetCachedModelsAsync source (don't re-apply CachedOnly) or verify FL Info.Cached is authoritative for that path.
- Remove when: M3 verifies FL Info.Cached semantics on the cached-models path.
- Status: **RESOLVED (M3)**. Fixed two ways: (1) `FoundryCatalogService.BrowseAsync` now strips `CachedOnly` from the effective filter and passes an `isCachedOverride` into `MapEnriched` so the cached-models path doesn't re-drop on `info.Cached`; `ListCachedAsync` passes `isCachedOverride:true`. (2) `CatalogGrouping.Partition` groups the UI by the **authoritative** cached-alias set from `ListCachedAsync` (not the per-model `IsCached` flag) — proven by `CatalogGroupingTests` (a model whose flag disagrees with the set is grouped by the set). Hardware e2e confirmed cached(1)/available(5) grouping with David's pre-cached qwen2.5-0.5b in the Installed/cached section.

### KI-010 — FL streaming emits no token usage; total-tokens shows honest "unknown" (M4)
- Area: FL integration (Foundry Local SDK 1.2.3 streaming).
- Symptom: during streaming chat, Foundry Local/Betalgo does NOT emit a `UsageContent`/usage frame, so `metric-total` (total tokens) honestly renders **"unknown"**. TTFT and tokens/sec are still real (measured from stream timing); finish reason IS provided (e.g. `length`→"max-tokens"). This is the honest outcome predicted by research R2 — NOT a bug or fabrication.
- Workaround: none needed — `FoundryMessageMapper.ExtractUsage` returns null when FL provides nothing and `TokenStatsAccumulator` surfaces "unknown" (no back-computed total). Hardware-verified on qwen2.5-0.5b.
- Remove when: a future FL build emits streaming usage; then `metric-total` will show the real count with no code change (the extraction already reads `Usage.TotalTokens`).
- Status: open (informational; honest by design).

### KI-011 — M4 live tool-calling UI deferred (needs tool-capable model + FL↔Betalgo tool bridge)
- Area: M4 chat scope decision.
- Symptom: US8 (live tool/function-calling UI) and US9 (structured-output toggle/regenerate) are NOT shipped in the first M4 increment. The MEAI pipeline (`UseFunctionInvocation` + `UseOpenTelemetry`) IS built and `ToolInvocationWiringTests` proves the middleware invokes a real `AIFunction`, but the full MEAI `AITool`→Betalgo `ToolDefinition` translation + FL tool-call-response→`FunctionCallContent` parsing is not wired into the live FL request, and no tool UI ships (no dead UI — Constitution III).
- Why deferred: end-to-end hardware verification needs a reliably tool-capable model; the only cached model (qwen2.5-0.5b) is too small to emit tool calls reliably, and downloading a larger model unprompted would consume time/bandwidth. Shipping unverified tool UI would violate the honesty constitution.
- Workaround: n/a — the capability is surfaced as absent, honestly. `Foundry/Tools/ChatTools.cs` (the 2 genuine tools) + the live FL tool bridge + the `chat-tool-activity` UI are the follow-up.
- Remove when: a follow-up wires the FL tool bridge and hardware-verifies the loop on a tool-capable model.
- Status: open (deferred follow-up).

### KI-012 — FL web service requires a bind URL in Configuration.Web at manager creation (M5)
- Area: FL integration (Foundry Local SDK 1.2.3).
- Symptom: `FoundryLocalManager.StartWebServiceAsync` fails with "Web service configuration was not provided" unless `Configuration.Web.Urls` is set when the singleton manager is created (`CreateAsync`). There is no runtime port argument — the bind address is fixed at manager-creation time.
- Resolution (M5): `FoundryLifecycle` sets `Configuration.Web = new Configuration.WebService { Urls = "http://127.0.0.1:5273" }`. The Server panel shows the ACTUAL bound URL from `manager.Urls` (verbatim), never a fabricated/assumed value. Localhost-only by design; no runtime port control (FL doesn't expose one).
- Remove when: n/a (documents the required FL configuration). If a configurable port is ever wanted, it requires recreating the singleton manager (out of v1 scope).
- Status: resolved (M5); informational.

### KI-013 — FL corrupts generation when `frequency_penalty` is set (chat output garbage)
- Area: FL integration (Foundry Local + Betalgo.Ranul.OpenAI streaming).
- Symptom: setting `frequency_penalty` on the chat request — **even to `0`** — makes Foundry Local emit degenerate output: an endless run of repeated characters (e.g. `.` or `?`) that never stops until max-tokens. Because the chat UI defaulted `FrequencyPenalty = 0` and sent it on every request, **every** chat reply was garbage; manually stopping was the only escape. Isolated by per-parameter hardware testing on phi-4-mini: `temperature` alone → clean ("Paris."), `top_p` alone → clean, **`frequency_penalty=0` alone → 256 chars of `.`**, no params → clean.
- Root cause: NOT our pipeline (per-token codepoint capture proved FoundryChatClient/ChatService pass correct Unicode). FL (or the FL↔Betalgo bridge) mishandles `frequency_penalty`.
- Workaround: never send `frequency_penalty`. Removed it from `InferenceParameters` (record + `ToChatOptions`), from `FoundryChatClient.ApplyInferenceSettings`, and from the Chat inference-parameter UI (honesty rule — no control for a param FL breaks on). Supported params are now temperature, max tokens, top-p only.
- Remove when: a future FL build honors `frequency_penalty` without corrupting generation; then re-add the param + UI control after hardware re-verification.
- Status: resolved in app (param removed); upstream FL issue to file.

### KI-014 — macOS 26 TCC SIGKILL: Bluetooth access without usage description
- Area: dotnet/macios + macOS 26 TCC (privacy) — surfaced via a linked system framework (WebKit/Essentials), NOT app code.
- Symptom: the app is intermittently hard-killed (EXC_CRASH / SIGKILL, `namespace: TCC`) with "attempted to access privacy-sensitive data without a usage description … must contain an NSBluetoothAlwaysUsageDescription key". Reproduced across builds on 2026-07-05 and 2026-07-08 (predates the Smart Search work — not caused by it). Intermittent (~3 kills across dozens of launches); the TCC check fires on an async XPC reply thread, so the crash stack shows only the TCC killer, not the original CoreBluetooth caller.
- Root cause: FoundryStudio has no Bluetooth feature. Some transitive native framework touches CoreBluetooth (likely WKWebView Web Bluetooth support or an Essentials probe); macOS 26 tightened TCC so a missing usage-description string is now a hard kill rather than a silent deny.
- Workaround: added `NSBluetoothAlwaysUsageDescription` to `src/FoundryStudio.App/Info.plist` (merged via `<PartialAppManifest>`), with an honest string stating the app does not use Bluetooth. This satisfies the static TCC requirement so the async check no longer SIGKILLs.
- Remove when: the offending framework stops probing CoreBluetooth, or we confirm the key is unnecessary on a future macOS/dotnet-macios build. If we ever genuinely add Bluetooth, replace the string with a real feature description.
- Upstream: not yet filed (dotnet/macios — needs a minimal repro to identify the CoreBluetooth caller).
- Status: worked around in app (Info.plist key); root-cause caller still unidentified.
