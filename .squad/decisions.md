# Squad Decisions

## Active Decisions

### DEC-001 — Workflow: Squad + spec-kit cohabitation
**Date:** 2026-06-25 · **Status:** Active
**Decision:**
- **spec-kit owns planning:** `/speckit.constitution`, `/speckit.specify`, `/speckit.plan`, `/speckit.tasks`.
- **Squad owns the implementation team:** specialists, reviewer-independence, async triage, this decisions log.
- **Bridge:** `/speckit.tasks` output is the input to Squad's coordinator; specialists pick tasks; decisions append back here.
**Rationale:** Squad alone has no prescribed planning structure; spec-kit alone has no enforced reviewer separation. Together they cover Karpathy's six steps.

### DEC-002 — Canonical reference
**Date:** 2026-06-25 · **Status:** Active
**Decision:** The canonical plan is **`docs/PLAN.md`** in this repo (hardened after a skeptic review and validated against MAUI.Sherpa). Supporting research: `docs/research/`. AGENTS.md, KNOWN-ISSUES.md, and KNOWN-GOOD-VERSIONS.md are binding guardrails. Until a feature is specced via `/speckit.specify`, `docs/PLAN.md` is the source of truth.

### DEC-003 — Methodology
**Date:** 2026-06-25 · **Status:** Active
**Decision:** Karpathy's six-step pattern ([blog](https://karpathy.bearblog.dev/sequoia-ascent-2026/)) is the operational frame: (1) define context, (2) tools, (3) feedback loop, (4) guardrails, (5) let agents work, (6) preserve human understanding. Each major feature spec runs through all six.

### DEC-004 — Architecture & scope (FoundryStudio)
**Date:** 2026-06-25 · **Status:** Active
**Context:** Confirmed via owner decisions, a skeptic review, and validation against [Redth/MAUI.Sherpa](https://github.com/Redth/MAUI.Sherpa) (a working MAUI + Blazor Hybrid + macOS AppKit app on the same packages).
**Decision:**
- **Stack:** `net11.0-macos` AppKit head (maui-labs) + Blazor Hybrid; macOS / Apple Silicon only in v1.
- **Foundry Local:** in-process SDK for catalog/model/chat via a thin `IChatClient` adapter (no loopback socket); the local OpenAI server is exposed for **external** tools only; one `FoundryLocalManager` singleton with a load/unload concurrency gate.
- **Scope:** v1 is the lighthouse core **M0 -> M4 + the M5 server toggle**; RAG/voice/presets/MCP/i18n are post-v1.
- **M0 is the linchpin gate** (toolchain pin -> FL dylib-chain bundling/signing -> BlazorWebView probe -> vertical slice). Nothing proceeds to M1 until M0 is green. Human-in-the-loop, not autonomous.
- **Capability honesty:** never ship UI for unsupported FL features (server auth/LAN, GGUF import, top_k/seed). Pin a known-good version set; upgrades re-run M0.
**Rationale:** Mac Catalyst can't load FL's `osx-arm64` dylib; AppKit can. Sherpa proves the stack + the native-bundling MSBuild pattern + hardened-runtime entitlements. See `docs/PLAN.md`.

### DEC-005 — M0-prep: Foundry Local package pin and native payload
**Date:** 2026-06-24 · **Status:** Proposed / confirm-on-hardware
**Decision:** For M0b, pin `Microsoft.AI.Foundry.Local` **1.2.3** (`sdk`, not `sdk_v2`) and do not pin the Windows-only `Microsoft.AI.Foundry.Local.WinML` package for macOS/AppKit. Treat `sdk_v2` runtime packaging as not yet public until `Microsoft.AI.Foundry.Local.Runtime` or a 2.x package is published.
**Native payload to bundle/sign:** `Microsoft.AI.Foundry.Local.Core.dylib` from `Microsoft.AI.Foundry.Local.Core` **1.2.3**, `libonnxruntime.dylib` from `Microsoft.ML.OnnxRuntime.Foundry` **1.26.0**, and `libonnxruntime-genai.dylib` from `Microsoft.ML.OnnxRuntimeGenAI.Foundry` **0.14.1**. Current public sdk line does not ship `libfoundry_local.dylib` or a separate Dawn dylib in the NuGet closure.
**Load-bearing citations:** Learn SDK reference (https://learn.microsoft.com/en-us/azure/foundry-local/reference/reference-sdk-current), Learn quickstart `CreateAsync` flow (https://learn.microsoft.com/en-us/azure/foundry-local/get-started), NuGet package (https://www.nuget.org/packages/Microsoft.AI.Foundry.Local), FL package index (https://api.nuget.org/v3-flatcontainer/microsoft.ai.foundry.local/index.json), FL package nupkg (https://api.nuget.org/v3-flatcontainer/microsoft.ai.foundry.local/1.2.3/microsoft.ai.foundry.local.1.2.3.nupkg), Core nupkg (https://api.nuget.org/v3-flatcontainer/microsoft.ai.foundry.local.core/1.2.3/microsoft.ai.foundry.local.core.1.2.3.nupkg), ORT Foundry nupkg (https://api.nuget.org/v3-flatcontainer/microsoft.ml.onnxruntime.foundry/1.26.0/microsoft.ml.onnxruntime.foundry.1.26.0.nupkg), ORT GenAI Foundry nupkg (https://api.nuget.org/v3-flatcontainer/microsoft.ml.onnxruntimegenai.foundry/0.14.1/microsoft.ml.onnxruntimegenai.foundry.0.14.1.nupkg), current SDK project (https://github.com/microsoft/Foundry-Local/blob/main/sdk/cs/src/Microsoft.AI.Foundry.Local.csproj), `sdk_v2` runtime nuspec (https://github.com/microsoft/Foundry-Local/blob/main/sdk_v2/cpp/nuget/Microsoft.AI.Foundry.Local.Runtime.nuspec), and releases mismatch note (https://github.com/microsoft/Foundry-Local/releases).
**Hardware gate:** Restore/build/run on Apple Silicon must confirm all three dylibs land in the `.app`, `otool -L` resolves, `FoundryLocalManager.CreateAsync()` works under hardened runtime, and whether library-validation disablement / nested re-signing is required.

### DEC-006 — M0-prep: scaffolding pins and bundle/sign structure
**Date:** 2026-06-24 · **Status:** Proposed / confirm-on-hardware
**Decision:** Use the proven MAUI.Sherpa `net10` known-good package set as the concrete baseline in `Directory.Packages.props`, keep the `net11` primary track explicitly TBD for M0a, and update the Foundry Local TBD notes with the DEC-005 proposed pins. Keep `build/BundleFoundryLocalNative.targets`, `Entitlements.Debug.plist`, and spike directories as M0 prep scaffolding only until David validates on Apple Silicon.
**Rationale:** `KNOWN-GOOD-VERSIONS.md` says not to float package versions; `docs/PLAN.md` says M0a pins net11 equivalents with Sherpa net10 as fallback; T002 required concrete non-floating baseline pins with clear TBD sections.

### DEC-007 — M0-prep: M0a baseline spike shape
**Date:** 2026-06-24 · **Status:** Proposed / confirm-on-hardware
**Decision:** The `spikes/m0a-baseline-app` author-only scaffold targets `net10.0-macos` pending T004 net11 staging, uses C# `MacOSBlazorWebView` root-component setup instead of XAML, and intentionally omits the Foundry Local bundle target because M0a contains no FL native payload.
**Hardware gate:** Final scaffold has not been built or run; David must validate with `dotnet build spikes/m0a-baseline-app -t:Run` and `maui devflow wait` on Apple Silicon before marking T005/M0a green.

### DEC-008 — M0a feasibility gate passed on Apple Silicon
**Date:** 2026-06-24 · **Status:** Active
**Decision:** M0a is GO for the fallback baseline: the pinned MAUI.Sherpa `net10.0-macos` AppKit + Blazor Hybrid set builds from clean restore and launches on real Apple Silicon under the installed net11 SDK/workloads. The native AppKit window opens, DevFlow connects, and Blazor renders live DOM (`m0a-shell` heading visually confirmed).
**Evidence:** `dotnet restore` + `dotnet build` succeeded; app launched with one 900x640 window; DevFlow `ui status` and `webview source` confirmed Blazor DOM; user visually confirmed heading.
**Follow-ups:** KI-001 records DevFlow screenshot's AppKit/WKWebView capture gap; KI-002 records DevFlow package feed correction. The net11-primary track remained open until DEC-013.
**References:** `specs/001-m0-feasibility-gate/contracts/gate-m0a-exit.md`; `spikes/m0a-baseline-app/`; `KNOWN-ISSUES.md` KI-001/KI-002.

### DEC-009 — M0b Foundry Local native-load gate passed
**Date:** 2026-06-24 · **Status:** Active
**Decision:** M0b, the true FL native-load GO/NO-GO, is GO: `Microsoft.AI.Foundry.Local` 1.2.3 loads in-process on real Apple Silicon, discovers WebGPU EP, downloads/loads `qwen2.5-0.5b`, streams a coherent reply, and unloads cleanly.
**Evidence:** Plain `net10.0` console, RID `osx-arm64`, restored/built with 0 warnings. Native chain copied and resolves: `Microsoft.AI.Foundry.Local.Core.dylib` 1.2.3, `libonnxruntime.dylib` 1.26.0, `libonnxruntime-genai.dylib` 0.14.1; `otool -L` showed only system deps and self `@rpath` IDs. No `libfoundry_local` or separate Dawn dylib is present.
**Scope boundary:** This console spike did not exercise hardened-runtime library validation; that risk moved to M0d and was resolved in DEC-011. Model cache is user data and must not be wiped.
**References:** `specs/001-m0-feasibility-gate/contracts/gate-m0b-exit.md`; `spikes/m0b-fl-console/`; `KNOWN-ISSUES.md` KI-003; Bishop T011.

### DEC-010 — M0c BlazorWebView capability probe complete
**Date:** 2026-06-25 · **Status:** Active
**Decision:** M0c is complete and non-blocking. File intake is not a platform gap: use `FilePicker.Default.PickAsync()` from MAUI Essentials (`Microsoft.Maui.Platforms.MacOS.Essentials`), not raw HTML `<input type=file>`. DevFlow DOM inspection is the primary AppKit Blazor verification path because screenshots do not capture the WKWebView layer.
**Findings:** Hot Reload could not be exercised with `dotnet watch` in this AppKit-head config; the app persisted only when the inner Mach-O binary was run directly. Record this as KI-004 and defer to M1's dedicated Hot Reload tooling.
**Evidence:** Real Apple Silicon `dotnet build` succeeded; DevFlow agent connected; `webview Runtime evaluate` confirmed DOM/handler injection; `dotnet watch` reproduced immediate-exit behavior and the static-web-assets path separator issue.
**References:** `specs/001-m0-feasibility-gate/contracts/gate-m0c-exit.md`; `KNOWN-ISSUES.md` KI-001/KI-004.

### DEC-011 — M0d vertical slice passed; M0 overall GO
**Date:** 2026-06-25 · **Status:** Active
**Decision:** M0d is GO, and M0 overall is GO. The signed hardened-runtime `.app` bundles the FL dylib chain into `Contents/MonoBundle`; FL Core loads, EP registration reaches 100%, catalog lists 46 models, `qwen2.5-0.5b` loads, and a streamed reply renders token-by-token in Razor through the in-process `IChatClient` adapter without a loopback socket.
**Server capability decision:** The exposed local OpenAI-compatible server supports tools/function calling: it returned structured `tool_calls` (`get_weather`, `{"city":"Paris"}`) with `finish_reason: tool_calls`. `response_format` is accepted but not enforced: `json_object` returned fenced JSON plus prose and `json_schema` was ignored. M4 may include tool-calling, but guaranteed structured output must be best-effort only or omitted.
**Design rules learned:** KI-005 requires FL async init off the UI dispatcher behind the ready gate with no `.Result`/`.Wait()`; KI-006 requires real MAUI Blazor `_Imports.razor` including `@using Microsoft.AspNetCore.Components.Web`. DevFlow Blazor UI interaction should use DOM `.click()` through `webview Runtime evaluate`.
**Evidence:** Real Apple Silicon `dotnet build` succeeded; native logs confirmed hardened-runtime FL init without `disable-library-validation`; DevFlow verified catalog/load/stream; server at `http://127.0.0.1:57140`; `curl` verified tools and response_format behavior.
**References:** `specs/001-m0-feasibility-gate/contracts/gate-m0d-exit.md`; `spikes/m0d-vertical-slice/`; `docs/PLAN.md`; `KNOWN-ISSUES.md` KI-003/KI-005/KI-006.

### DEC-012 — M0d implementation shape and ownership notes
**Date:** 2026-06-25 · **Status:** Active
**Decision:** The M0d vertical slice spike is a throwaway proof head adapted from the proven M0a AppKit + Blazor shape, importing `build/BundleFoundryLocalNative.targets` to exercise FL dylib bundling and using a lazy async `FoundryReadyService.ReadyAsync()` gate. The `FoundryChatClient` is a minimal MEAI `IChatClient` adapter over Foundry Local streaming; `ChatOptions` mapping remains M1 work.
**Evidence/Citations:** Implementation followed the M0b proven FL chat sequence (`spikes/m0b-fl-console/Program.cs`), upstream `FoundryLocalManager`/`Configuration` server APIs (`Configuration.Web.Urls`, `StartWebServiceAsync`, `StopWebServiceAsync`, `Urls`), upstream FL catalog/model contracts, and dotnet/extensions MEAI abstractions.
**Boundary:** Bishop authored the spike shape and handed off hardware validation; DEC-011 records David/coordinator's real Apple Silicon validation results.
**References:** `spikes/m0d-vertical-slice/`; `build/BundleFoundryLocalNative.targets`; upstream Foundry Local SDK source; MEAI abstractions source.

### DEC-013 — Move to net11.0-macos; T004 closed
**Date:** 2026-06-25 · **Status:** Active
**Decision:** FoundryStudio's forward track is `net11.0-macos`; the net11-primary toolchain is proven and supersedes the net10 fallback. Use the installed SDK `11.0.100-preview.5.26302.115` via `global.json` (`rollForward: latestPatch`, `allowPrerelease: true`); do not install a new SDK or macos-maui-dogfood Hot Reload CI build for this move.
**Pinned proven version set:** maui-labs `Microsoft.Maui.Platforms.MacOS`, `.BlazorWebView`, `.Essentials` = `0.26.0-dev` from `/Users/davidortinau/work/LocalNuGets`; `Microsoft.Maui.Controls` and `Microsoft.AspNetCore.Components.WebView.Maui` = `11.0.0-preview.4.26230.3` from the dnceng dotnet10 feed; Debug-only `Microsoft.Maui.DevFlow.Agent`/`.Blazor` = `0.25.0-dev` from LocalNuGets; FL pins remain `Microsoft.AI.Foundry.Local` 1.2.3 + ORT.Foundry 1.26.0 + ORT GenAI.Foundry 0.14.1 from nuget.org. `NuGet.config` uses localnugets + dotnet10 + nuget.org with packageSourceMapping; the net10 Sherpa set stays only as a disabled fallback reference.
**Evidence:** Both M0a empty app and M0d FL slice were retargeted to `net11.0-macos`, restored, built, launched, and inspected on real Apple Silicon. DevFlow reported TFM `net11.0-macos` and MAUI framework 11.0.0; FL CreateAsync, catalog, load, and stream smoke remained green.
**Effect:** M1 starts on `net11.0-macos`; M1 spec/plan text that still says `net10.0-macos` must be corrected at kickoff.
**References:** SentenceStudio `src/SentenceStudio.MacOS`; `NuGet.config`; `global.json`; `Directory.Packages.props`; `KNOWN-GOOD-VERSIONS.md`.

### DEC-014 — Competitive positioning
**Date:** 2026-06-25 · **Status:** Active
**Decision:** Do not position FoundryStudio as a general-purpose local-LLM runner or "LM Studio but Microsoft." Concede the hobbyist run-anything/GGUF lane and compete as the on-device client for the Foundry platform: curated/trusted models, ONNX/NPU optimization, governance, transparent OTel-no-PII telemetry, clean local server, and path to Foundry cloud.
**Wedges:** LM Studio's structural gaps are closed-source/verifiability, model-import/folder friction, paywalled enterprise governance, resource opacity, and REST API stability. FoundryStudio can exploit those with openness/transparency if OSS is chosen, FL-managed cache, Azure/Entra governance alignment, honest RAM-fit UX, and an in-process `IChatClient` architecture.
**Corrections:** LM Studio is free for commercial/work use since July 2025 except Enterprise; do not use price as the wedge. LM Studio now ships speculative decoding, so do not frame it as missing.
**Open lever:** Whether FoundryStudio ships open-source remains undecided; OSS + transparent telemetry is the cleanest wedge but is not yet committed.
**References:** `docs/PLAN.md` Competitive positioning; lmstudio.ai/blog/free-for-work; `github.com/microsoft/Foundry-Local/issues`.

### DEC-015 — Independent /review cleared M0 + net11 changeset for purpose
**Date:** 2026-06-25 · **Status:** Active
**Decision:** The independent pre-push review found the M0/net11 changeset sound for its stated purpose: hardware-proven M0 feasibility spikes, deliberate net11 toolchain pinning, and spec-kit M1 artifacts. Reviewer independence was satisfied because the reviewer authored none of the changes.
**Cleared:** No secrets/PII; `.DS_Store` ignored; in-process `IChatClient` honored; KI-005 off-dispatcher init implemented; no `.Result`/`.Wait()`; `global.json`, packageSourceMapping, disabled net10 group, Debug entitlements, and constitution/spec artifacts are acceptable.
**Fixed immediately:** Removed stray `spikes/m0d-vertical-slice/.gitkeep`; added `@using Microsoft.AspNetCore.Components.Web` to M0a `_Imports.razor` for KI-006 consistency.
**M1 backlog recorded as KI-007:** (1) LocalNuGets absolute path + mutable `-dev` pins must become portable/immutable before real CI restore; (2) streaming must guard empty `chunk.Choices`; (3) transitive `Betalgo.Ranul.OpenAI` dependency must be declared/pinned or isolated; (4) faulted `Lazy<Task>` init must reset on failure; (5) native dylib copy currently runs after codesign and must be folded into the signing pipeline for M1/M7.
**Gate status:** Clean for committing the spike evidence; KI-007 items are M1-bound, not M0/net11 blockers.
**References:** `/review`; `KNOWN-ISSUES.md` KI-007.

### DEC-016 — M1 App Shell + FL Service Layer COMPLETE (autonomous run; net11.0-macos)
**Date:** 2026-06-25 · **Status:** Active
**Context:** Built autonomously (David away) from specs/002-m1-app-shell-foundation. Targets **net11.0-macos** (per the ratified net11 move, DEC of 2026-06-25 — supersedes the net10 baseline written in the M1 spec/plan/tasks).
**Decisions made in David's absence:**
- D1: M1 on net11.0-macos (Core/Foundry/Tests net11.0; App net11.0-macos).
- D2: KI-007 fixes applied in code — empty-Choices stream guard + standard Delta fallback (FoundryMessageMapper); Betalgo isolated to that single internal mapper + pinned explicitly (9.1.0); faulted-init RETRY in FoundryLifecycle (not Lazy-memoized).
- D5: file-based `FileSettingsService` (human-readable JSON, consent-gated, .bak recovery) in Foundry with app-data path injected by the App — keeps MAUI out of the managed layer (spec said "Preferences"; a JSON file better serves FR-014 auditability).
- D6: post-v1 stubs live in Core (FL-free, just throw) so they're testable in the Core-only test project.
**Delivered (4-project solution replacing the throwaway spikes):** FoundryStudio.Core (FL-free seams/abstractions/DTOs/concurrency gate/catalog+RAM-fit/settings/post-v1 stubs), FoundryStudio.Foundry (FoundryLifecycle, FoundryCatalogService, in-process FoundryChatClient IChatClient adapter, ChatService, FileSettingsService), FoundryStudio.App (AppKit+Blazor head, AppReadyBoundary gate, DI), FoundryStudio.Tests (Core-only, dylib-free). One FoundryLocalManager singleton + ReadyAsync gate off-dispatcher (KI-005, no .Result/.Wait); one ModelStateGate (drain/reject/serialize/per-model isolation); in-process chat (no loopback); consent-gated settings; full _Imports (KI-006); lock files + seam CI gate.
**Review:** independent code-review CLEAN — no Critical/High; faulted-retry race, drain race, and mutate-then-lease ordering all confirmed correct. Two shutdown-only dispose-race nits deferred to M2 (KI-008).
**Open follow-ups:** KI-007 #1 (LocalNuGets/-dev portability — App build is self-hosted; seam CI gates the rest); KI-008 (M2 dispose hardening); catalog metadata/variants TODO(M2); cache-delete UI is M3; tools(yes)/structured-output(best-effort) wiring is M4.
**Verified:** Real Apple Silicon, net11.0-macos — `dotnet build FoundryStudio.sln` 0 errors; **31/31 xUnit pass dylib-free** (no-blocking-init guard, concurrency gate, catalog filter, RAM-fit, settings, post-v1 stubs); locked-mode restore valid on the seam (CI gate); app launches → AppReadyBoundary "initializing" → "ready" with no deadlock (DevFlow DOM); in-process chat smoke streamed a real reply with **0 active loopback connections** (`lsof`, SC-006).

### DEC-017 — M2 Catalog browse + discovery COMPLETE (autonomous; net11.0-macos)
**Date:** 2026-06-25 · **Status:** Active
**Delivered:** the first real UI — a browsable/searchable/filterable catalog on the M1 foundation. Core enrichment: ModelInfo extended (SizeGb double?, Device Device?, +ExecutionProvider/ContextLength/MaxOutputTokens/License/Publisher/ModelType/ModelCapabilities) mapped from real FL 1.2.3 IModel.Info+Variants (FileSizeMb->GB, Runtime.DeviceType/ExecutionProvider, Task, ProviderType, ContextLength, License, SupportsToolCalling, InputModalities->vision); honest nullable (absent FL field -> "unknown", never fabricated/0GB). New Core pure seams: CapabilityParser, CuratedSelector (documented allow-list, no quality claim), CatalogFacets. Catalog UI (Blazor): list page @page "/" + ModelCard + search + device/task/provider filters + cached-only toggle + curated default + honest loading/empty/error states; consumes only IFoundryCatalogService + Core seams (UI never touches FL SDK); WCAG AA (labels, text+icon cached badge, aria-live count, role/aria states).
**Browse-only (verified):** ZERO download/load/unload/delete/variant-select/chat affordances; no FL mutation from any browse path.
**Review:** independent /review — mapping honesty + seams + negative invariant + a11y all confirmed; one Medium honesty bug FIXED (curated banner gated on IsShowingCurated so it no longer mislabels filtered full-catalog results) + Empty-state fix; KI-009 (CachedOnly double-filter) recorded as M3 watch.
**Verified:** net11.0-macos, real Apple Silicon — dotnet build sln 0 errors; 40/40 xUnit pass dylib-free (incl. CapabilityParser/CuratedSelector/CatalogFacets); locked restore valid; app launches->ready->catalog renders 6 curated of 46 with REAL metadata (qwen2.5-0.5b 0.7GB GPU/WebGPU ctx 32768 tool-calling apache-2.0 cached; sizes 0.7-5.2GB); search 'phi'->7 from 46 (banner correctly hidden); cached-only filter->1 cached; 0 mutation affordances (DevFlow DOM). KI-001 confirmed: WebView pixel screenshot needs window frontmost (DOM is the by-design evidence path); KI-001 updated with root cause.

### DEC-018 — M3 Model install & management COMPLETE (autonomous; net11.0-macos)
**Date:** 2026-06-26 · **Status:** Active
**Delivered:** the first MUTATING UI — install/load/unload + **consent-gated delete** on the M2 catalog. Core pure seams (dylib-free, unit-tested): `DiskFitHeuristic` (Evaluate(size?,free)->Fits/Warn/Unknown, 1.25 safety factor, honest Unknown for null size), `CatalogGrouping.Partition` (groups by the **authoritative** cached-alias set, not per-model flag — KI-009 resolution), `VariantSelectionState` (HasVariants/PinnedVariantId/EffectiveVariantId; defaults to first, ignores unknown pins), `ConsentGuard.RequireConfirmed` (the single throw-gate for destructive ops, dylib-free so the consent gate is unit-testable). Service surface (additive): `variantId` param threaded through `IFoundryCatalogService.DownloadAsync/LoadAsync` -> `ResolveTargetAsync` -> FL `IModel.SelectVariant`. `FoundryCatalogService.BrowseAsync` strips CachedOnly + uses `isCachedOverride` in MapEnriched (KI-009). UI (Blazor, consumes only IFoundryCatalogService/ISettingsService + Core seams): ModelCard management actions (download w/ progress+cancel+auto-load, load/unload + loaded indicator), `ConfirmDialog` (names model + GB + "cannot be undone", role=dialog/aria-modal, focus-trap, Esc, **defaults focus to Cancel**), cached/available grouping, variant select, disk-fit warning (per-effective-variant size), Settings page with configurable cache dir.
**Decisions made in David's absence:**
- D1: Extracted `ConsentGuard` to Core (FL-free) so Constitution-IV consent is provable by a Core-only unit test (DeleteConsentGateTests) — the gate throws before any FL/native work.
- D2: Grouping keys off `ListCachedAsync` (authoritative) not `info.Cached` — closes KI-009 at the source AND in the view layer.
- D3: Delete is strictly two-step — the card delete button ONLY opens the dialog; `DeleteFromCacheAsync(...,userConfirmed:true)` is reachable ONLY from the dialog confirm handler. No one-click destructive path (independent review confirmed).
**Review:** independent code-review (reviewer independence: coordinator wrote Core, Hicks wrote UI, neither approved own work) — consent gate / KI-009 / KI-005 / honesty / variant-id flow all CLEAN, no Critical/High. 3 findings FIXED + re-verified: [Med] disk-fit now reads the effective-variant size (was parent size); [Low] dialog defaults focus to Cancel (data-preservation); [Low] progress bar starts 0% not 100% while waiting.
**Verified:** net11.0-macos, real Apple Silicon — `dotnet build FoundryStudio.sln` 0 errors; **51/51 xUnit pass dylib-free** (+DiskFitHeuristic/CatalogGrouping/VariantSelectionState/DeleteConsentGate; KI-005 no-blocking-init guard still green after catching+fixing a `.Result` Hicks introduced in Home.razor init); app launches->ready->catalog groups **Installed/cached(1) + Available(5)** with David's pre-cached qwen2.5-0.5b intact; **US2 load/unload** proven (loaded badge + "Loaded: qwen2.5-0.5b" then unload restores state, non-destructive); **US3 consent gate** proven e2e — delete opens dialog naming "qwen2.5-0.5b? This frees 0.7 GB and cannot be undone", **Cancel preserves the model (cached count stays 1)**, dialog defaults focus to Cancel; Settings shows real cache path /Users/davidortinau/Library/FoundryLocal/models. **DATA PRESERVATION HONORED: David's cached model never deleted** (only ever opened+cancelled the dialog; no live delete performed). Screenshots: docs/evidence/m3-catalog-management.png + m3-delete-consent-dialog.png (window frontmost per KI-001).
**Open follow-ups:** live-delete path (RemoveFromCacheAsync) not exercised against a real download (would need a throwaway model download; consent gate + unit test cover the invariant); download-progress determinate bar not visually confirmed mid-download (no large download performed to protect time/bandwidth — wiring + cancel verified by DOM); tools(yes)/structured-output wiring + streaming chat is M4.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
