# Phase 0 Research: M1 — App Shell + Foundry Local Service Layer + DI + Test/CI Seam

M1 has **no open `NEEDS CLARIFICATION`** — the spec's Clarifications section records "No
outstanding clarifications," and the design space is already narrowed by completed M0 work.
Phase 0 therefore consolidates the **established decisions M1 builds on** (cited, not
re-litigated) and resolves the handful of M1-specific design choices. Each entry follows
Decision / Rationale / Alternatives considered.

---

## Established by M0 (cited as proven — do NOT re-litigate)

These are inputs, not open questions. Listed so the design is auditable.

- **E1 — Stack**: `net10.0-macos` AppKit head (maui-labs `Microsoft.Maui.Platforms.MacOS{,.BlazorWebView,.Essentials}` `0.1.0-preview.8.26256.5`) + Blazor Hybrid, central package management. *Source: DEC-004, `KNOWN-GOOD-VERSIONS.md`, `Directory.Packages.props`, M0a.* net11 is the separate open chore **T004** (Assumptions; FR-019) — a prerequisite risk, not an M1 blocker.
- **E2 — Foundry Local SDK identity & API**: `Microsoft.AI.Foundry.Local` `1.2.3` (`sdk` line, not `sdk_v2`); transitive `.Core` 1.2.3 + ORT.Foundry 1.26.0 + ORT GenAI.Foundry 0.14.1. Confirmed API: `CreateAsync(Configuration, ILogger)` / `Instance` / `IsInitialized`; `DiscoverEps` / `DownloadAndRegisterEpsAsync`; `GetCatalogAsync → ICatalog` (`ListModelsAsync` / `GetModelAsync`); `IModel.IsCachedAsync` / `DownloadAsync` / `IsLoadedAsync` / `LoadAsync` / `UnloadAsync` / `GetChatClientAsync`; `StartWebServiceAsync` / `StopWebServiceAsync` / `Urls`. *Source: DEC-005, M0b/M0d, `spikes/m0d-vertical-slice/Services/*`.*
- **E3 — Native dylib chain bundling/signing**: the FL dylib chain (`Microsoft.AI.Foundry.Local.Core.dylib` + `libonnxruntime.dylib` + `libonnxruntime-genai.dylib`) bundles into the signed `.app` `Contents/MonoBundle` via `build/BundleFoundryLocalNative.targets` and loads under hardened runtime **without** `com.apple.security.cs.disable-library-validation` (`Entitlements.Debug.plist` as-is). KI-003 install-name is benign. *Source: M0b/M0d evidence, `Entitlements.Debug.plist`, `build/BundleFoundryLocalNative.targets`.*
- **E4 — Server capability (M0d finding)**: tools/function-calling = **SUPPORTED** (structured `tool_calls`); `response_format` = **ACCEPTED but NOT ENFORCED** (no constrained decoding) → structured output is **best-effort only**, never "guaranteed JSON." *Source: M0d gate exit.* M1 encodes capability honesty around this (FR-018); the exposed server itself is M5.

---

## R1 — Where the ready-gate lives and how it offloads init (KI-005)

- **Decision**: Promote the M0d `FoundryReadyService` pattern into `FoundryStudio.Foundry.FoundryLifecycle`, implementing `IFoundryLifecycle` from Core. Keep the exact proven shape: a `Lazy<Task<FoundryLocalManager>>` whose factory is `Task.Run(InitializeAsync)`, exposing `Task ReadyAsync(ct)` and `Task<FoundryLocalManager> GetManagerAsync(ct)`. Components await `ReadyAsync()` in `OnInitializedAsync`, then `await InvokeAsync(StateHasChanged)`. Registered as a DI singleton; implements `IAsyncDisposable` to dispose the manager on app exit.
- **Rationale**: This is the literal KI-005 mitigation already proven on hardware in `spikes/m0d-vertical-slice/Services/FoundryReadyService.cs` (`Task.Run` keeps FL's heavy synchronous native-load off the BlazorWebView dispatcher) and `Components/Pages/Home.razor` (`InvokeAsync(StateHasChanged)`). `Lazy<Task<…>>` guarantees init runs exactly once and all awaiters observe the same instance (FR-003, SC-002). The hard "no `.Result`/`.Wait()`" rule is structural: nothing in the gate blocks. *Source: KI-005, M0d service + component.*
- **Alternatives considered**: `IHostedService`/`BackgroundService` startup — rejected; the MAUI AppKit head has no generic-host start hook on this path and the `Lazy<Task>` gate already gives single-init + await semantics with less machinery. Awaiting `CreateAsync` directly in `OnInitializedAsync` — rejected (it is the KI-005 freeze). A manual `TaskCompletionSource` — rejected; `Lazy<Task>` is the minimal correct primitive.

## R2 — Concurrency gate design (drains-or-rejects, per-model, serialized)

- **Decision**: A pure primitive **`ModelStateGate` (implements `IModelStateGate`) in `FoundryStudio.Core.Concurrency`**, FL-free so it is unit-testable without a dylib. It tracks, per model id, (a) a count of active streaming generations and (b) a mutation lock. `BeginGenerationAsync(modelId)` returns an `IAsyncDisposable` lease that increments/decrements the active count. `MutateAsync(modelId, Func<Task> op, MutationPolicy policy)` either **drains** (awaits active leases to reach zero, honoring a bound) or **rejects** (throws a typed `ModelBusyException` with an honest, actionable message) when a stream is active, and serializes concurrent mutations on the same model via a per-model `SemaphoreSlim`. Mutations on a *different* model are not blocked (per-model isolation). `FoundryCatalogService` routes every `LoadAsync`/`UnloadAsync` through `MutateAsync`, and the chat path wraps each stream in a `BeginGenerationAsync` lease.
- **Rationale**: PLAN.md line 75 / DEC-004 require this contract designed in M1, drains-or-rejects against an active stream **on the same model**, serialized. Keeping it a pure primitive satisfies Story 2's "through the service layer alone (no UI)" independent test and FR-016/SC-008 (testable without native dylib). The M0d `SliceCatalogService` already used a `SemaphoreSlim(1,1)` load gate — this generalizes it to per-model with active-stream awareness. The rejection is a typed exception, never a fake success or silent no-op (FR-010, SC-004). *Source: PLAN.md 75, DEC-004, spec edge cases, `spikes/m0d-vertical-slice/Services/SliceCatalogService.cs` `_loadGate`.*
- **Alternatives considered**: A single global lock across all models — rejected; the spec explicitly allows load/unload on a *different* model during a stream (edge case). Cancelling the in-flight stream on mutation — rejected; that tears a user/external generation (the native-crash failure mode the contract exists to prevent). Optimistic mutate + retry — rejected; racing native state is exactly the crash risk.

## R3 — In-process `IChatClient` adapter, no socket

- **Decision**: Port `spikes/m0d-vertical-slice/Services/FoundryChatClient.cs` into `FoundryStudio.Foundry.FoundryChatClient : IChatClient` (Microsoft.Extensions.AI), backed by `IModel.GetChatClientAsync()` and `CompleteChatStreamingAsync`. Map MEAI `ChatMessage` ↔ the FL request message type (the M0d adapter used `Betalgo.Ranul.OpenAI` request types transitively from the FL SDK). Expose it behind `IChatService`, and register it so MEAI middleware can compose: `chatClient.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()` (wiring deferred to M4, seam reserved now). Each streaming call acquires a `ModelStateGate` generation lease (R2). **No `127.0.0.1` socket** is opened for the in-process chat path (SC-006).
- **Rationale**: DEC-004 and PLAN.md lines 59–62, 74 mandate the in-process adapter to kill the port-binding / SSE-chunk-boundary failure surface; M0d proved the adapter streams real tokens in-process. The adapter is the "real architecture" the slice already mirrored, so promotion is a move, not a rewrite. *Source: DEC-004, PLAN.md 74, M0d `FoundryChatClient`.*
- **Alternatives considered**: Route chat through the exposed local server (`StartWebServiceAsync` + loopback HTTP) — rejected by DEC-004 (false dichotomy; reintroduces the avoided failures; the server is for external tools only). Wait for the FL SDK to implement `IChatClient` natively — rejected; it does not, and the thin adapter is the documented bridge.

## R4 — Settings store: Preferences + auditable JSON, never-wipe-without-consent

- **Decision**: `ISettingsService` (Core) over `AppSettings` (record: `ModelCacheDirectory`, `DefaultModel`, `Theme`). The **pure logic** — documented defaults, JSON (de)serialization, merge-missing-with-defaults, and a `RequireConsent` guard that prevents destructive overwrite/clear without an explicit consent flag — lives in `FoundryStudio.Core.Settings.SettingsDocument` (FL-free, fully unit-tested). The **platform binding** — persisting the JSON via MAUI Essentials `Preferences` (key) plus a human-readable JSON file in app data — lives in `FoundryStudio.Foundry.PreferencesSettingsService`. On missing/empty/corrupt read, apply documented defaults **without** destroying prior data (non-destructive recovery: keep a `.bak` of an unparseable file).
- **Rationale**: PLAN.md line 95 specifies "Preferences + JSON"; Constitution IV + FR-014/015 + SC-007 require auditable, user-editable, consent-gated, never-silently-wiped settings, and the cache directory points at multi-GB protected user data. Splitting pure logic from the platform Preferences call keeps the store unit-testable with no MAUI runtime (SC-008). *Source: PLAN.md 95, Constitution IV, FR-014/015.*
- **Alternatives considered**: Opaque binary/`Preferences`-only blob — rejected; not human-auditable/user-editable (FR-015). A database (SQLite) — rejected; over-scoped for three settings and not trivially user-editable. Wiping-and-rewriting on schema change — rejected outright (data preservation).

## R5 — RAM-fit heuristic as a pure seam

- **Decision**: `FoundryStudio.Core.Catalog.RamFitHeuristic` is a pure function: inputs model size (GB) and free RAM (GB); output a `RamFitResult` (e.g., `Comfortable` / `Tight` / `Unlikely`) computed as model-size vs **free** RAM with a wide margin and a "long chats use more (KV-cache grows with context)" caveat flag. It renders no UI and produces no confident green verdict.
- **Rationale**: PLAN.md line 101 warns a size-vs-total-RAM badge "will be wrong often"; the spec/Assumptions place the heuristic as a pure-logic seam in M1 and its UI/badge in M2. Pure + deterministic = directly unit-testable without a dylib (FR-016, SC-008). Capability/UX honesty: the heuristic exposes uncertainty, it does not promise "will run." *Source: PLAN.md 101, spec Assumptions, FR-016.*
- **Alternatives considered**: Total-RAM-based green/red badge — rejected (PLAN.md 101: OOM-kills 7B at long context on 16 GB). Querying live native memory in M1 — rejected; out of scope, and the heuristic must stay dylib-free for the test seam.

## R6 — Catalog filtering as a pure seam

- **Decision**: `FoundryStudio.Core.Catalog.CatalogFilter` holds pure predicates over FL-free `ModelInfo`/`ModelVariant` DTOs (filter by device CPU/GPU/NPU, task, provider; expose variants; partition cached vs loaded lists). `FoundryCatalogService` maps FL `ICatalog`/`IModel` results into these DTOs, then applies the pure filters — so filtering is unit-tested without FL.
- **Rationale**: FR-011 requires the catalog service to expose browse/variants/cached/loaded; FR-016/SC-008 require catalog **filtering** to be a dylib-free test seam. Mapping FL types → DTOs at the service boundary keeps the filter logic pure and the rest of the app coding against stable Core types. *Source: FR-011/016, PLAN.md 99–101.*
- **Alternatives considered**: Filtering directly over FL `IModel` instances — rejected; couples the test seam to the SDK and the native dylib.

## R7 — Project layout that guarantees dylib-free tests

- **Decision**: Four projects: FL-free `FoundryStudio.Core` (interfaces + DTOs + pure seams + gate primitive), FL-bound `FoundryStudio.Foundry` (FL impls + adapter + stubs), `FoundryStudio.App` (`net10.0-macos` Razor head, imports the bundle target + entitlements), and `FoundryStudio.Tests` referencing **Core only**. The bundle target is imported **only** by the head.
- **Rationale**: FR-016 + SC-008 + the edge case "Unit tests run with no native dylib present" demand the pure seams test without the FL dylib. Referencing only the FL-free Core from the test project makes that structural and citeable — independent of any assumption about whether the FL managed assembly loads dylib-free. *Source: FR-016, SC-008, spec edge cases.*
- **Alternatives considered**: A single Core that references the FL SDK (3 projects total) — rejected for the test seam: it would transitively pull the FL managed assembly into the test project, making "no native dylib present" an assumption rather than a guarantee (Constitution I: avoid uncited confidence). Putting FL impls in the App head only — rejected; M2–M5 and tests need the services as a shared library, not locked in the macOS head.

## R8 — Post-v1 service stubs that don't fake behavior

- **Decision**: `IEmbeddingService`, `ITranscriptionService`, `ILocalServerService` are defined in Core now; their `FoundryStudio.Foundry.PostV1.Stub*` implementations throw `NotSupportedException("Not implemented in v1 …")` (or expose an explicit `IsSupported => false`) rather than returning fake/empty results. Registered in DI so the dependency graph is stable for M5/M6.
- **Rationale**: FR-013 + the edge case require non-faking stubs; Constitution IV forbids shipping behavior for capabilities not implemented. Defining the interfaces now lets later milestones implement without reshaping DI (spec Assumptions). *Source: FR-013, Constitution IV.*
- **Alternatives considered**: Omit the interfaces until their milestone — rejected; reshaping the graph later churns every consumer. No-op stubs returning empty data — rejected; that fakes behavior (capability dishonesty).

## R9 — Blazor `_Imports` and template fidelity (KI-006)

- **Decision**: `FoundryStudio.App/_Imports.razor` includes the full MAUI Blazor template using-set: `Microsoft.AspNetCore.Components`, `…Components.Web`, `…Components.Forms`, `…Components.Routing`, `…Components.Web.Virtualization` (as needed), `Microsoft.JSInterop`, plus app namespaces. `EnableDefaultRazorItems=false` with explicit `RazorComponent` includes (Sherpa shape).
- **Rationale**: KI-006 — omitting `…Components.Web` makes `@onclick`/`@bind` silently compile as literal attributes (no build error); FR-002/SC-012 require the full set. M0d caught this; M1 starts from the correct template `_Imports`. *Source: KI-006, FR-002, `spikes/m0d-vertical-slice/_Imports.razor` (which was the minimal/buggy set — M1 uses the full set).*
- **Alternatives considered**: Carry the M0d `_Imports` verbatim — rejected; it lacked Forms/JSInterop; M1 uses the complete template set.

## R10 — CI: clean-checkout restore+build+test on pinned versions

- **Decision**: One GitHub Actions job on a macOS Apple-Silicon runner: clean checkout → `dotnet restore` → `dotnet build` the solution → `dotnet test FoundryStudio.Tests`, all against the pinned `Directory.Packages.props`. Enforce no-float by enabling central package management lock files / `--locked-mode` (or failing the build if restore would resolve off the pinned set). The job is the guardrail that defends the multi-preview stack against silent churn.
- **Rationale**: FR-017 + SC-009 + Constitution Engineering Culture ("never see a red X") require a clean-checkout build on pins that fails if a dependency floats. M1 builds a `-macos` head, so the build leg needs a macOS runner; the pure-logic tests would also run on Linux, but keeping one macOS job covers both build + test simplest. *Source: FR-017, SC-009, Constitution.*
- **Alternatives considered**: Split Linux (test) + macOS (build) jobs — viable but more machinery than M1 needs; one macOS job satisfies the gate. Floating to "latest" with a dependabot bump — rejected; violates the pinning rule (every bump re-runs M0).

## R11 — Disposition of the M0 spikes

- **Decision**: Once M1 scaffolds the real solution, **delete/archive** `spikes/m0a-baseline-app`, `spikes/m0b-fl-console`, and `spikes/m0d-vertical-slice` (and prune `spikes/` to its README noting they were throwaway and where the patterns now live). Their proven patterns are carried into the real projects (R1–R3, R9), not refactored in place.
- **Rationale**: They were declared throwaway in the M0 spec and DEC-006/007; the spec's FR-001 says M1 "replaces the disposable M0 spikes." Removing them keeps the repo surgical and avoids two competing app shapes (Constitution III). *Source: FR-001, DEC-006/007, `spikes/README.md`.*
- **Alternatives considered**: Keep the spikes alongside the real app — rejected; duplicate app heads invite drift and confuse the build/CI surface.

## Output

All M1 design questions resolved; **no `NEEDS CLARIFICATION` remain**. Established M0 facts
(E1–E4) are cited inputs. Proceed to Phase 1 (data-model, contracts, quickstart).
