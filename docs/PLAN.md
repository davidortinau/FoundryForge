# Implementation Plan — Foundry Local desktop client (.NET MAUI, AppKit + Blazor Hybrid)

> Working codename: **FoundryStudio** (placeholder — rename freely).
> An LM Studio-style desktop client for Microsoft Foundry Local, built as a native macOS (AppKit) .NET MAUI app with a Blazor Hybrid UI, also exposing Foundry Local's OpenAI-compatible local server.

## Why / success measure

- **Why:** Give .NET developers (and David's dogfood loop) a first-party-feeling, on-device AI client on macOS that browses the Foundry Local catalog, manages models, chats locally, and serves an OpenAI-compatible endpoint to other tools — proving MAUI-on-AppKit (maui-labs) as a serious desktop target and exercising the on-device AI client story for MAUI.
- **We'll know it's successful when:** on an Apple Silicon Mac we can launch the app, browse the catalog, download + load a model, hold a streaming chat, toggle the local server on and have an external tool (e.g. `curl`/Open WebUI) hit `http://127.0.0.1:<port>/v1/chat/completions` successfully — all offline after first download.

## Confirmed decisions (this session)

| Decision | Choice |
|---|---|
| UI framework | **Blazor Hybrid** (BlazorWebView) |
| Host backend | **maui-labs AppKit** macOS head (`Microsoft.Maui.Platforms.MacOS` + `.BlazorWebView`) |
| v1 platforms | **macOS only** (Apple Silicon, `osx-arm64`); Windows/WinUI is a documented fast-follow |
| .NET version | **net11.0-macos** (preview) — David's call. Sherpa's net10 patterns (native-bundling target, entitlements, package set) port to net11; M0a pins the net11-compatible AppKit build via the `macos-maui-dogfood` skill (stages .NET 11 Preview MAUI CI packages + pinned `global.json`/`NuGet.config`). |
| FL integration | **In-process SDK** for catalog/model/chat **+** expose the **local OpenAI-compatible server** |
| Scope | **Lighthouse core** — v1 = M0→M4 (catalog → load → streaming chat) **+ the M5 server toggle as the "wow."** RAG / voice / presets / MCP / i18n are **post-v1**. Reframed from "full parity" per skeptic review (LM Studio is a multi-year product; "parity in one push" by one person on an unproven foundation = many half-features). |
| Distribution | **Internal dogfood — runs only from a local `dotnet build -t:Run` on the dev's own Mac.** Handing a zipped `.app` to a colleague needs `xattr -dr com.apple.quarantine` + a `disable-library-validation` entitlement (unsigned third-party dylibs); not truly distributable until signed/notarized (post-v1). |
| Repo | **`~/work/FoundryStudio`** — new standalone git repo (flat under `~/work/`, matching your convention); consumes maui-labs AppKit packages via the **LocalNuGets feed** (`~/work/LocalNuGets/`) and/or project references into `~/work/maui-labs` during dev |

## Feasibility verdict: LIKELY — architecture proven by MAUI.Sherpa; one FL-specific native spike remains

The earlier "de-risked, YES" was overstated; the skeptic's "UNKNOWN" was right at the time. **[Redth/MAUI.Sherpa](https://github.com/Redth/MAUI.Sherpa) now resolves most of it** — a real, working **.NET MAUI + Blazor Hybrid + macOS AppKit** app on the same maui-labs packages, by a MAUI engineer. It proves the stack builds, runs, renders rich Blazor UI, **and bundles a native `osx-{arch}/native` payload into the `.app` with a custom MSBuild target** — the exact problem we feared. See "Reference architecture" below.

**Now confirmed (Sherpa + research):**
- The MAUI + Blazor Hybrid + AppKit stack **works today** (Sherpa ships it on `net10.0-macos`, SDK `10.0.301`). We target net11; Sherpa's patterns port.
- **Native-payload bundling is a solved pattern** — Sherpa's `_BundleCopilotCliForMacOS` target copies `runtimes/{rid}/native/*` into `Contents/MonoBundle/runtimes/{rid}/native/` because "the macOS build system doesn't copy it into the `.app` bundle automatically." We adapt this for FL's dylibs.
- **Hardened runtime + entitlements is a solved pattern** — Sherpa runs `EnableHardenedRuntime=true` with a Debug entitlements file that already grants `network.server` (our exposed-server feature) and `allow-jit` (ORT). 
- Mac Catalyst avoidance correct; auth none; Apple-Silicon only; WebGPU/Metal accel registers internally.

**Residual unknowns (M0 — now well-scoped, not open-ended):**
1. **FL is a dylib *chain*, not a single executable.** Sherpa bundles one exec'd CLI binary; FL is `libfoundry_local` + ORT + ORT-GenAI + Dawn, `dlopen`'d into the process with inter-dylib `@rpath`. M0b must confirm the whole chain resolves inside `MonoBundle` and whether `dlopen`'d dylibs need `com.apple.security.cs.disable-library-validation` (Sherpa's exec'd CLI doesn't hit library validation; ours will).
2. **net11 AppKit package maturity.** Sherpa is proven on net10 AppKit `0.1.0-preview.8.26256.5`; the net11 AppKit build is newer/less battle-tested. M0a pins and smoke-tests the net11 set.
3. **Does the FL local server honor `tools` + `response_format`?** Go/no-go for M4 (confirm in M0d).

## Reference architecture — MAUI.Sherpa (proven; adopt its patterns)

[Redth/MAUI.Sherpa](https://github.com/Redth/MAUI.Sherpa) `src/MauiSherpa.MacOS` is our build template. Adopt:

- **Project shape:** `Microsoft.NET.Sdk.Razor`, `net10.0-macos` (we use net11), `UseMaui=true`, `SingleProject=true`, `SupportedOSPlatformVersion=14.0`, explicit `RazorComponent` includes (`EnableDefaultRazorItems=false`), multi-head (Core lib + macOS head + shared services via linked `Compile`), `wwwroot` via `BundleResource`, `PartialAppManifest` for Info.plist privacy strings.
- **Native-payload bundling (the M0b pattern):** a `Target AfterTargets="Build"` that copies `$(OutDir)runtimes/$(rid)/native/<payload>` → `$(OutDir)$(ApplicationTitle).app/Contents/MonoBundle/runtimes/$(rid)/native/` (+ `chmod +x` for execs). We generalize this to copy the **FL dylib chain** (`libfoundry_local`, ORT, ORT-GenAI, Dawn) and verify inter-dylib `@rpath` resolves.
- **Signing:** `EnableHardenedRuntime=true` + `CodesignEntitlements=Entitlements.Debug.plist` (Debug). Sherpa's Debug entitlements: `app-sandbox=false`, `cs.allow-jit=true`, `network.client=true`, **`network.server=true`** (our exposed local server), `get-task-allow=true`. We **add `com.apple.security.cs.disable-library-validation`** (FL dylibs are `dlopen`'d, unlike Sherpa's exec'd CLI) if M0b shows it's needed.
- **Known-good package set (net10 baseline → pin net11 equivalents in M0a):** AppKit `Microsoft.Maui.Platforms.MacOS{,.BlazorWebView,.Essentials}` `0.1.0-preview.8.26256.5`; `Microsoft.Maui.Controls 10.0.41`; `Microsoft.AspNetCore.Components.WebView.Maui 10.0.1`; **DevFlow** `Microsoft.Maui.DevFlow.{Agent,Blazor} 0.1.0-preview.7.26230.1` (Debug-only); `Markdig 0.44.0` (markdown), `Microsoft.AspNetCore.Components.QuickGrid`, `Shiny.Mediator.Maui` (mediator), `Sentry.Maui` (errors). These resolve from public nuget.org (Sherpa has no repo-root NuGet.config). For net11 we pin the net11 AppKit build via `macos-maui-dogfood`.
- **DevFlow on this exact stack is proven** (Sherpa wires `DevFlow.Agent` + `DevFlow.Blazor`, Debug-only) — confirms our verification loop works for Blazor Hybrid on AppKit.

## Architecture

```
macOS .app (net11.0-macos, AppKit host via Microsoft.Maui.Platforms.MacOS)
└── BlazorWebView (Microsoft.Maui.Platforms.MacOS.BlazorWebView)
    └── Razor components (the UI) — run in-process (Blazor Hybrid, no server circuit)
        └── inject app services (MAUI DI):
            ├── IFoundryCatalogService   → FoundryLocalManager + ICatalog + IModel  (in-process)
            │     browse / download / load / unload / delete / variants / BYOM
            ├── IChatService             → chat + tools + telemetry
            │     PRIMARY: in-process FL SDK wrapped in a thin (~100-line) IChatClient
            │       adapter so MEAI middleware composes WITHOUT a socket:
            │       .AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()
            │     (no loopback HTTP for our own chat — that was a false dichotomy)
            ├── IEmbeddingService        → FL OpenAIEmbeddingClient  (powers RAG, post-v1)
            ├── ITranscriptionService    → FL Whisper + LiveAudioTranscriptionSession (voice, post-v1)
            ├── ILocalServerService      → StartWebServiceAsync / StopWebServiceAsync / Urls
            │     exposes /v1/* to EXTERNAL tools ONLY (the LM Studio "server" feature)
            └── settings / presets / per-model config / chat history  (app-level persistence)

FoundryLocalManager (singleton) — one instance backs BOTH the in-process UI path
and the externally-exposed server. Guarded by an explicit concurrency contract.
```

Key integration notes (revised per skeptic review):
- **Chat is in-process, not over a socket.** The FL SDK doesn't implement `IChatClient`, but we get MEAI middleware (function-calling, OpenTelemetry, caching, DI) by writing a **thin in-process `IChatClient` adapter** over `OpenAIChatClient`/`model.GetChatClientAsync()`. This kills a whole failure surface (port binding, SSE chunk-boundary correctness, the server having to be *running* for chat to work) vs. routing our own chat through `127.0.0.1`. The local server exists **only** to serve external tools.
- **Singleton concurrency contract (required, not optional).** One `FoundryLocalManager` backs the UI and the exposed server. A load/unload while an external client is mid-stream on the same model will tear the generation or crash natively. Implement a **load/unload gate** that drains or rejects in-flight generations before mutating model state; never call load/unload during an active stream on that model. Design this in M1, don't discover it in M5.
- **Async-ready gate (required, not a UX nicety).** `CreateAsync()` is async; `CreateMauiApp()` is sync; Blazor components first-render before init finishes. Define a `Task<FoundryLocalManager> ReadyAsync()` that **every** service awaits, an app-level "initializing" route that blocks the chat UI until ready, and a hard rule: **no `.Result`/`.Wait()`** on the init task (deadlocks the UI thread). Part of M1.
- **Native bundle reality:** getting `runtimes/osx-arm64/native/*.dylib` (the whole chain) into `Contents/MonoBundle` and loading under library validation is the M0 question — likely needs a custom MSBuild copy target + `disable-library-validation` entitlement + re-signing nested dylibs. Do not assume it's automatic.

## Milestones

### M0 — Staged feasibility gates (LINCHPIN — each gate is go/no-go before the next)
Do **not** build app code until M0d passes. Each gate fails cheap.

- **M0a — Toolchain + version pin (before any code).** Use the **`macos-maui-dogfood` skill** to stage the .NET 11 Preview MAUI CI packages + pinned `global.json`/`NuGet.config`/.csproj props. Pin the **net11-compatible** AppKit `Microsoft.Maui.Platforms.MacOS{,.BlazorWebView,.Essentials}` build + matching MAUI 11 preview + macOS SDK band into `Directory.Packages.props` + `KNOWN-GOOD-VERSIONS.md`. Fallback reference if a net11 AppKit build is unstable: Sherpa's proven net10 set (`0.1.0-preview.8.26256.5` / MAUI `10.0.41`). Gate: a Sherpa-shaped empty app builds + launches on net11 before feature work.
- **M0b — FL native-load spike in a *console* head (no MAUI/Blazor).** `dotnet new macos` console, add `Microsoft.AI.Foundry.Local`, build, then **adapt Sherpa's `_BundleNativeForMacOS` target** to copy the FL dylib chain into `Contents/MonoBundle/runtimes/osx-arm64/native/`; `find *.app -name '*.dylib'` + `otool -L` to confirm the **whole chain** (`libfoundry_local` + ORT + ORT-GenAI + Dawn) lands and `@rpath`s resolve; `CreateAsync` + one inference. If `dlopen` fails library validation, add `disable-library-validation` to entitlements (+ re-sign nested dylibs). **The project's true go/no-go** — but now a scoped adaptation of a proven target, not an open question. `mcp-binlog-tool` on the binlog shows exactly what copies.
- **M0c — BlazorWebView capability probe.** Sherpa proves Blazor Hybrid renders rich UI on the AppKit head (Markdig markdown, QuickGrid) — so core viability is *confirmed*. Residual checks only: **file drag-in to `<input type=file>`** (WKWebView sandboxes file access) and Hot Reload. If file intake fails, post-v1 RAG/voice need a native MAUI file-shim — know now.
- **M0d — Vertical slice.** AppKit + BlazorWebView app: in-process `CreateAsync` → list catalog → download+load `qwen2.5-0.5b` → stream one reply in a Razor component. Also verify **the FL server honors `tools` + `response_format`** (go/no-go for M4).
- **Verify (DevFlow):** real Apple-Silicon end-to-end; no `DllNotFoundException`; streamed reply renders.

### M1 — App shell + FL service layer + DI + test/CI seam
- Solution scaffold: AppKit host project + Razor UI, shared services, models. Generate `AGENTS.md` + `KNOWN-ISSUES.md` + `KNOWN-GOOD-VERSIONS.md`; run `repo-bootstrap`.
- `FoundryLocalManager` lifecycle service with the **`ReadyAsync()` gate** (every service awaits it; app-level "initializing" route; no `.Result`/`.Wait()`), dispose on exit.
- The **singleton concurrency gate** (load/unload drains/rejects in-flight generations) — implemented here, not deferred.
- `IFoundryCatalogService` (wraps `ICatalog`/`IModel`); **in-process `IChatClient` adapter** over the SDK; `IEmbeddingService`/`ITranscriptionService`/`ILocalServerService` interfaces (impls stubbed for post-v1 ones).
- App settings store (cache dir, default model, theme) via Preferences + JSON.
- **Tests + CI from day one:** an `xUnit` project covering the pure-logic seams behind the `IFoundry*` interfaces (settings, catalog filtering, RAM heuristic, later RAG chunking) — these need no native dylib. One CI job: restore + build the solution on a clean checkout against the **pinned** versions (defends the four-preview stack against silent churn).

### M2 — Catalog browse + discovery
- Catalog list UI: search, filter by device (CPU/GPU/NPU) / task / provider, curated default view.
- Per-model card: alias, displayName, size (GB), device/EP, context length, capabilities (vision/tool/reasoning), license, cached badge, variants.
- **Memory fit indicator (not a confident "will it run").** A green/red badge from file-size + total RAM will be wrong often — KV-cache growth with context length (not file size) dominates runtime memory, plus unified-memory pressure and the WebGPU working set. Show **"model size vs *free* RAM"** with a wide margin and a "long chats use more" caveat; do not render a confident green verdict that will OOM-kill a 7B at long context on 16GB.
- 6-hour catalog cache awareness + manual refresh; offline state handling.

### M3 — Model install & management
- Download with progress (`DownloadAsync` callback), cancel; auto-load option.
- Load / unload (`LoadAsync`/`UnloadAsync`), loaded-state polling, currently-loaded indicator.
- Delete from cache (`RemoveFromCacheAsync`) with confirm; cached vs available grouping.
- Variant selection (pin a specific quantization/device). Configurable model cache directory.
- BYOM: import an Olive-compiled ONNX model (drop into `ModelCacheDir` + `inference_model.json`); guided flow + docs link. (Note: ONNX-only; **no GGUF import** — see Parity Map.)
- Disk-space check before download.

### M4 — Chat experience (v1 core)
- Multi-turn streaming chat via the **in-process `IChatClient` adapter** (no socket), markdown rendering, code blocks w/ copy.
- System prompt (per-chat), inference params surfaced by FL `ChatSettings`: temperature, max tokens, top_p, frequency_penalty. (top_k/min_p/repeat_penalty/seed not in FL — see Parity Map.)
- Token stats (TTFT, tokens/sec, total), context-window tracker w/ warnings.
- Tool/function calling via MEAI `UseFunctionInvocation()` (app-defined .NET tools) — **gated on M0d confirming the FL path honors `tools`**; if not, descope tool-calling from v1.
- Structured output (JSON schema via `response_format`) **only if M0d confirms support**; otherwise omit (don't ship a fake toggle).
- Chat history (folders, new/clear/duplicate), conversations persisted to app data.

> **— v1 line: M0→M4 above is the shippable lighthouse core. M5 below ships its server *toggle* in v1; M6/M7 are post-v1. —**

### M5 — Local server feature (the LM Studio "server")
- `StartWebServiceAsync`/`StopWebServiceAsync`; bind `127.0.0.1:<port>` (configurable port).
- Server panel: start/stop toggle, status, actual bound URL (`Urls`), copy endpoint, list exposed `/v1/*` routes, live request log (FL logs).
- Document FL server constraints: **localhost-only, no auth, no LAN bind** (FL doesn't support token auth / `0.0.0.0` today) — surface clearly rather than fake it.

### M6 — Advanced features (POST-v1)
- **Embeddings + RAG:** drag-in docs (PDF/txt/md), chunk + embed via FL embeddings, retrieve, inject. (Depends on M0c proving WKWebView file intake; else needs a native MAUI file-shim.)
- **Voice input:** mic capture → FL Whisper (`LiveAudioTranscriptionSession`) → insert transcript.
- **Config presets:** bundle system prompt + params; save/load/export JSON.
- **Per-model default config:** persisted load/inference defaults per model.
- **Themes** (dark/light/auto) + basic i18n scaffold.
- **MCP host (stretch):** app-level MCP client (not an FL feature).

### M7 — Signed/distributable packaging + polish (POST-v1)
- v1 runs only from local `dotnet build -t:Run` on the dev's own Mac. Real distribution = Developer ID signing + hardened runtime + notarization (the `disable-library-validation` entitlement + re-signed nested FL dylibs are the hard part) — **out of v1**, sequenced here.
- First-run experience, empty states, error toasts (download failure / corrupt cache / OOM-on-load), settings screen.
- DevFlow session review on any long/stuck loops → opt-in product feedback to maui-labs.
- README + honest "how to run" (build-locally-only for v1; quarantine/entitlement note for sharing).

## LM Studio parity map (what FL gives us vs. gaps)

| LM Studio capability | Foundry Local support | Plan |
|---|---|---|
| Catalog browse/search/filter | ✅ `ListModelsAsync` + rich `ModelInfo` + CLI-style filters | M2 |
| Download w/ progress, manage, delete | ✅ `DownloadAsync`/`Remove`/`IsCached`/cached+loaded lists | M3 |
| Variant (quant/device) selection | ✅ `Variants` / `GetModelVariantAsync` | M3 |
| Import external model | ⚠️ **ONNX-only via Olive/BYOM**; no GGUF/safetensors import | M3 (documented limit) |
| Streaming chat | ✅ in-process + server | M4 |
| Inference params | ⚠️ temp/max_tokens/top_p/frequency_penalty only (no top_k/min_p/repeat_penalty/seed) | M4 (documented limit) |
| Tool/function calling | ✅ via FL server + MEAI `UseFunctionInvocation` | M4 |
| Structured JSON output | ⚠️ server **accepts** `response_format` but does **not enforce** it (M0d: json_object → markdown-fenced JSON + prose; json_schema → ignored). No constrained decoding. Ship as best-effort only, never "guaranteed JSON" | M4 (descope the guarantee) |
| Local OpenAI server | ✅ `StartWebServiceAsync` (`/v1/*`) | M5 |
| Server auth + LAN bind | ❌ FL is localhost-only, no token auth | M5 (surface as not-supported) |
| Embeddings / RAG | ✅ FL embeddings; RAG is app-level | M6 |
| Voice transcription | ✅ FL Whisper / live session | M6 |
| Presets / per-model config | ➕ app-level (FL has prompt templates in metadata) | M6 |
| Themes / languages | ➕ app-level | M6 |
| MCP host | ❌ not FL; app-level MCP client | M6 stretch |
| Speculative decoding / parallel batching | ❌ not exposed by FL (LM Studio shipped spec-decoding in 0.3.10; users report it's hit-or-miss / often slower) | out of scope |
| Auto-update / notarized installer | n/a v1 (dogfood) | future |

## Competitive positioning (vs LM Studio / Ollama / Jan)

> Grounded in current (2025–2026) developer sentiment from the LM Studio bug tracker, Reddit, and comparison reviews, plus Foundry Local's own GitHub issues. Two corrections to stale assumptions: **LM Studio became free for commercial/work use in July 2025** (only its Enterprise tier — SSO, model gating, private sharing — is paid), and **LM Studio now ships speculative decoding**. Do not pitch "we're free, they cost money" or treat spec-decoding as a unique LM Studio edge.

**The reframe — we are not a general-purpose local-LLM runner.** On the single most-wanted capability (run *any* GGUF from HuggingFace), we lose decisively and it is **not fixable at our layer** — we inherit Foundry Local's curated, ONNX-only, Microsoft-gated catalog. A hobbyist who wants tonight's new HF model picks LM Studio or Ollama. We do not compete for that buyer. **FoundryStudio is the on-device client for the Foundry platform**: trusted/curated models, NPU/ONNX optimization, governance, and a path to Foundry cloud. That framing turns the curated catalog from a *limitation* into a *feature* (trust/compliance) and concedes the "run-anything" crowd on purpose.

**Where we look weak to someone comparing side by side (and whose limit it is):**

| Gap | Severity | Owner |
|---|---|---|
| No arbitrary GGUF / HF models (curated ONNX catalog only) | 🔴 Critical | Foundry Local — product feedback to Maanav/Meng, not our debt |
| macOS-only in v1 (LM Studio is Win/Mac/Linux) | 🔴 Critical for breadth | Our scope choice |
| No RAG / document chat at v1 (M6) | 🟠 High | Our scope |
| No model-import breadth (BYOM is ONNX/Olive only) | 🟠 High | Foundry Local |
| No voice at v1 (M6) | 🟡 Med | Our scope |
| Limited sampling params (no top_k/min_p/repeat_penalty/seed) | 🟡 Med | Foundry Local |
| No speculative decoding | 🟢 Low (hit-or-miss in practice) | Foundry Local |

Most reds/oranges trace to **Foundry Local**, not FoundryStudio — they are product-feedback bullets, not app engineering debt.

**LM Studio complaints = our opportunities (current, sourced):**

1. **Closed source + unverifiable privacy/telemetry** — LM Studio's most-cited criticism and a real enterprise-compliance blocker. The FL backend is already open. **If FoundryStudio ships open-source with our constitution's "no PII in logs, OpenTelemetry-only" telemetry, that's a wedge LM Studio structurally cannot match.** (OSS for FoundryStudio is an open decision — see DEC entry — currently v1 is build-locally dogfood.)
2. **Model import / directory friction** (manual folder placement, surprise re-downloads, broken symlinks) — we sidestep it entirely; FL manages the cache. "Models just work, no folder archaeology" is a demo-able contrast.
3. **Enterprise governance is paywalled in LM Studio** (SSO / model gating / private sharing = paid Enterprise). Foundry Local + Entra/Azure could offer **model gating + governance natively** as a differentiator, not an upsell.
4. **Resource/memory opacity** — recurring complaint. Lean into honest RAM-fit UX + clear EP/device visibility (see M2 memory-fit badge, rated as a *size-vs-free-RAM* indicator, not a confident verdict).
5. **REST API stability gripes** — our in-process `IChatClient` (no loopback for our own chat) + the exposed server is architecturally cleaner. Reliability as a feature.

**Positioning one-liner for the README / pitch:** *"The on-device tier of the Foundry platform — trusted models, transparent and open, governed, on Apple Silicon."* Not "LM Studio but Microsoft."

## Reference implementation
- **Closest first-party reference:** `microsoft/Foundry-Local` `samples/js/electron-chat-application` — full GUI (model sidebar download/load/delete, streaming chat w/ token stats, context tracker, Whisper voice, markdown). Use its feature list + UX as the v1 spec; we reimplement in Blazor Hybrid against the C# SDK. No MAUI + FL prior art exists today (confirmed via search).

## Risks / watch-items (re-rated after MAUI.Sherpa evidence)
1. **[SERIOUS, was BLOCKER] FL dylib *chain* load** — Sherpa proves native-payload bundling into the `.app` works (single CLI exe); residual is the multi-dylib `@rpath` chain + `disable-library-validation` for `dlopen`. M0b adapts Sherpa's target; well-scoped now.
2. **[SERIOUS, downgraded] net11 AppKit package maturity** — Sherpa is proven on net10 AppKit `preview.8`; net11 build is newer. M0a pins it via `macos-maui-dogfood`; net10 set is the fallback reference. (net11 is David's active dogfood track.)
3. **[SERIOUS] FL server `tools`/`response_format` support unconfirmed** — gated in M0d; in-process `IChatClient` adapter removes the loopback-HTTP failure surface for our own chat.
4. **[SERIOUS] Single `FoundryLocalManager` contention** — concurrency gate is M1, not M5.
5. **[MINOR, downgraded] BlazorWebView viability** — Sherpa proves rich Blazor Hybrid UI on AppKit; only file drag-in + Hot Reload remain to check (M0c).
6. **[MINOR] Pinned-preview drift** — freeze the Sherpa-derived known-good set; upgrades are deliberate chores that re-run M0.
7. **[MINOR] Memory-fit badge will mislead** — show size-vs-free-RAM, not a confident verdict.
8. **[MINOR] Unsigned dogfood `.app` won't run on other Macs** (Gatekeeper) — v1 is build-locally-only.
9. **[MINOR] Async-init race** — `ReadyAsync()` gate + no `.Result`, designed in M1.
10. **[STRATEGIC] Effort/payoff** — lighthouse core (M0→M4 + M5 toggle) maximally serves the "prove MAUI-on-AppKit + on-device AI" goal; the backend bugs M0–M4 surface are the real payoff (David also owns that backend).

## Verification approach (per global rules)
- Every UI-bearing change verified with **MAUI DevFlow**: `dotnet build -t:Run` → `maui devflow wait` → screenshot/inspect/interact, read native logs on failures (no uninstall-to-reset).
- M0 and each milestone close with an **end-to-end check on a real Apple Silicon Mac**, not just build success: model download + load + streamed reply (and for M5, an external `curl` to the exposed endpoint).
- A `Verified:` line on every milestone-closing summary.

## Tooling & skills

**Mandatory loop (per global rules):** every running-app change is built/deployed/inspected via **MAUI DevFlow**; `/review` runs before any push; an end-to-end check runs before any "done".

| Purpose | Tools / skills |
|---|---|
| **Build · run · verify loop** | `maui-devflow-onboard` (add DevFlow to the new project), `maui-devflow-debug` (build → deploy → inspect → fix on the running app), `maui-devflow-session-review` (turn stuck loops into opt-in maui-labs feedback), `maui devflow` CLI, `dotnet build -t:Run`, `hotreload-sentinel` (when Blazor Hybrid Hot Reload misbehaves) |
| **MAUI engineering guidance** | `.NET MAUI Guidance` agent, `maui-current-apis` (always-on API-currency guardrail), `maui-app-lifecycle` (async FL init at startup), `maui-dependency-injection`, `maui-hybridwebview`/BlazorWebView patterns, `maui-file-handling` (model cache + RAG doc intake), `maui-media-picker` + `maui-permissions` (mic), `maui-speech-to-text` (mic capture paired with FL Whisper), `maui-secure-storage`/preferences (settings + gated-model tokens), `maui-theming`, `maui-localization`, `maui-performance`, `maui-accessibility`, `maui-app-icons-splash`, `maui-visual-review`, `maui-unit-testing` |
| **Code intelligence & editing** | `lsp` (C# goToDefinition / findReferences / rename / hover), grep / glob / view / edit, `task` subagents (`explore` for codebase research, `general-purpose` for multi-step, `research` for upstream digs) |
| **Docs & API truth** | `learndocs` MCP (Microsoft Learn: FL, MAUI, MEAI), `context7` MCP (library docs), `github-mcp-server` (read FL + maui-labs source), `mihubot` (search dotnet repos), `web_search`/`web_fetch` |
| **Build diagnostics** | `mcp-binlog-tool` (analyze the MSBuild binlog — especially M0, to confirm whether/why `runtimes/osx-arm64/native/*.dylib` copy into the `.app`), DevFlow native logs |
| **Server & UI testing** | `playwright` (drive a browser/Open WebUI against the exposed `/v1` endpoint; optional Blazor UI smoke), `curl` for the M5 external-endpoint check |
| **Repo hygiene & review** | `repo-bootstrap` (make `~/work/FoundryStudio` Copilot-ready: copilot-instructions + skills), `git-commit`, `review` / `deep-review` (pre-push gate), `validate`, `skill-builder` (if we author a project "foundry-local-client" skill) |
| **Escalation / contacts** | `workiq` — only to reach FL owners (**Maanav Dalal** IC / **Meng Tang** portfolio) when filing upstream issues |

## When an upstream dependency, tool, or skill falls short

Default posture: **never block — ship a documented workaround, file upstream, remove the workaround when the fix lands.**

1. **Confirm it's real first (RTFM gate).** Reproduce minimally, check the latest package version, and **read the actual source** (Foundry-Local, maui-labs, dotnet/maui, dotnet/macios, dotnet/extensions) before concluding a gap or bug. Verify negative claims ("no API exists", "the dylib can't load") against source/docs — don't guess. On a slow loop (full rebuild + macOS deploy), **read source before iterating**; cap "let me try" attempts and switch to reading the dylib/MSBuild source.
2. **Classify by ownership and act:**
   - **David-owned** (maui-labs AppKit backend, Comet, DevFlow, `maui` CLI, `Maui.Essentials.AI`): fix it directly in `~/work/maui-labs`, build a local package into `~/work/LocalNuGets/`, consume it, dogfood — as a small dedicated commit. (Sole Comet maintainer; free hand in maui-labs.)
   - **Foundry Local** (`microsoft/Foundry-Local`): apply a local workaround to stay unblocked (e.g., `NativeLibrary.SetDllImportResolver`, custom MSBuild copy of the `osx-arm64` dylibs into the bundle, pin `sdk` vs `sdk_v2`), **and** file a precise issue with a minimal repro + cited source line; route to **Maanav Dalal** (fast IC) / **Meng Tang** (escalation). Surface capability gaps (server auth, LAN bind) as product feedback, not as fake features.
   - **Other upstreams** (dotnet/maui, dotnet/macios, MEAI, Velopack, etc.): local shim + pin a known-good version + file an upstream issue with repro and cited source.
3. **Record every workaround** in `KNOWN-ISSUES.md` + an inline code comment linking the tracking issue, so it's removable on fix. Use `maui-devflow-session-review` to convert any long/stuck DevFlow loop into opt-in maui-labs feedback.
4. **Skills/tools (Copilot CLI):** if a skill is wrong or stale, fix/extend it with `skill-builder` and reconcile its reference files in the same turn; if a tool lacks a capability, search for an alternative (`tool_search_tool_regex`) or fall back to `bash`/`gh`.

## AGENTS.md for the new repo (focus + effectiveness guardrails)

Generate `~/work/FoundryStudio/AGENTS.md` during M1 scaffolding using the `agents` skill, seeded with the content below, and keep it in sync as decisions change. It exists to keep every coding agent (and us) focused. Draft content:

- **What this is (one line):** LM Studio-style desktop client for Foundry Local — native macOS (AppKit) .NET MAUI app, Blazor Hybrid UI, also exposing Foundry Local's local OpenAI server.
- **Non-negotiable architecture:** AppKit head (`Microsoft.Maui.Platforms.MacOS`, maui-labs) · Blazor Hybrid (BlazorWebView) · `net11.0-macos` · in-process FL SDK for catalog/model/chat **+** the exposed local server · **one `FoundryLocalManager` singleton** backs both surfaces (never construct a second).
- **Build / run / verify (mandatory loop):** `dotnet build -t:Run`; every running-app change goes through **MAUI DevFlow** (`maui-devflow-debug`); read native logs to diagnose, **never uninstall to reset**; an **end-to-end check on real Apple Silicon** (download → load → streamed reply; server → external `curl`) precedes any "done"; run `/review` before any push.
- **Scope boundaries (stay focused):** **macOS / Apple-Silicon only in v1** — do not add iOS/Android/**Mac Catalyst** code paths or RIDs (Mac Catalyst is the one path that doesn't work; AppKit is deliberate). Models are **ONNX-only** (no GGUF/safetensors import). **Do not fake unsupported Foundry Local capabilities** — server auth, LAN bind, `top_k`/`min_p`/`seed`, speculative decoding don't exist in FL; surface them as limits, don't stub fake UI.
- **Data preservation:** the model cache is user data (multi-GB). Never wipe it to "reset"; back up before any destructive cache operation; prefer a settings toggle over deletion.
- **Dependency discipline:** pin the FL package (`sdk` vs `sdk_v2` — confirm which is current and record it); consume maui-labs AppKit packages from the **LocalNuGets feed** (`~/work/LocalNuGets/`); when an upstream falls short, follow the workaround→file→`KNOWN-ISSUES.md` policy in this plan (fix maui-labs/Comet/DevFlow yourself; file Foundry-Local issues to Maanav Dalal / Meng Tang).
- **Known constraints to respect:** async `FoundryLocalManager.CreateAsync()` must complete before first inference — gate the UI on a ready-state; first run needs network to download; everything is offline after.
- **Code conventions:** C# nullable enabled, MAUI DI container for services, Razor components call injected services directly (Blazor Hybrid = in-process, no server circuit / no `HttpContext`), keep platform-specific code out of shared UI.

Add a `KNOWN-ISSUES.md` alongside it for workaround tracking (each entry links its upstream issue and is removed when fixed).

## Open questions (non-blocking; assumptions noted)
- App/codename + bundle id (assuming placeholder `FoundryStudio` / `com.example.foundrystudio`).
- Repo name/location for the new standalone repo.
- Whether MCP host stays in v1 (currently a stretch in M6).
