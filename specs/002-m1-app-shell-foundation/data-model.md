# Phase 1 Data Model: M1 — App Shell + Foundry Local Service Layer + DI + Test/CI Seam

M1 introduces one persisted application data store (app settings) plus several **in-memory
domain entities and DTOs** that define the service-layer seams. FL SDK types
(`FoundryLocalManager`, `ICatalog`, `IModel`) are wrapped, not redefined; the entities below are
the **FL-free shapes** the app, tests, and later milestones code against. Persisted/protected
user data is flagged.

Conventions: entities live in `FoundryForge.Core` unless noted. "FL-free" = no
`Microsoft.AI.Foundry.Local` reference, so it is unit-testable with no native dylib (FR-016,
SC-008).

---

## Entity: AppSettings  *(persisted — protected user data)*

The user-editable, auditable configuration. Located in `Core/Models`.

| Field | Type | Notes |
|---|---|---|
| ModelCacheDirectory | string (path) | Points at the multi-GB FL model cache (protected user data). Default: documented platform path. |
| DefaultModel | string? | Alias of the default model (e.g., `qwen2.5-0.5b`); null until set. |
| Theme | enum `Light` \| `Dark` \| `Auto` | Default `Auto`. |
| SchemaVersion | int | For non-destructive forward migration. |

**Persistence**: JSON document on disk (human-readable, user-editable) keyed/anchored via MAUI
Essentials `Preferences`. Bound by `FoundryForge.Foundry.PreferencesSettingsService`; pure
shape + (de)serialization + merge-with-defaults in `Core/Settings/SettingsDocument`.

**Validation / rules**:
- Reading unset/missing/empty/corrupt settings returns **documented defaults** and never
  destroys prior data; an unparseable file is preserved (e.g., `settings.json.bak`) before
  defaults are written (FR-014, SC-007, edge case "Settings file is missing/empty/corrupt").
- No write may clear or destructively overwrite persisted settings (or the cache directory they
  reference) without an explicit per-action consent flag — enforced by the `RequireConsent`
  guard (FR-015, Constitution IV).
- Contents are auditable and user-editable (plain JSON), never opaque (FR-015).

**State transitions**: `unset → set` (user writes a value) and `set → set` (edit). There is no
`→ wiped` transition without consent.

---

## Entity: ModelInfo  *(in-memory DTO, FL-free)*

The app-facing projection of an FL catalog model. Mapped from `IModel`/catalog results at the
`FoundryCatalogService` boundary so filtering/heuristics stay FL-free.

| Field | Type | Notes |
|---|---|---|
| Alias | string | Stable model alias (e.g., `qwen2.5-0.5b`). |
| Id | string | Concrete model/variant id. |
| DisplayName | string | Human-friendly name. |
| SizeGb | double | On-disk size; input to the RAM-fit heuristic. |
| Device | enum `Cpu` \| `Gpu` \| `Npu` | Execution target (filter dimension). |
| Task | string | e.g., `chat`, `embedding` (filter dimension). |
| Provider | string | Catalog provider/source (filter dimension). |
| Variants | ModelVariant[] | Available quantizations/devices (FR-011 variants). |
| IsCached | bool | Present in the local cache. |
| IsLoaded | bool | Currently loaded in the manager. |

**Validation**: `Alias`/`Id` non-empty; `SizeGb ≥ 0`. `IsCached`/`IsLoaded` reflect live FL
state at projection time (the service re-projects; the DTO is a snapshot).

---

## Entity: ModelVariant  *(in-memory DTO, FL-free)*

A selectable quantization/device variant of a model (FR-011 "variants").

| Field | Type | Notes |
|---|---|---|
| VariantId | string | Concrete variant id. |
| Quantization | string? | e.g., `int4`, `fp16`. |
| Device | enum `Cpu` \| `Gpu` \| `Npu` | Variant execution target. |
| SizeGb | double | Variant on-disk size. |

---

## Entity: CatalogFilter  *(pure-logic seam, FL-free)*

Filter criteria + the pure predicate that applies them to a `ModelInfo[]`. Unit-tested without
FL (FR-016, SC-008).

| Field | Type | Notes |
|---|---|---|
| Device | enum? | Optional CPU/GPU/NPU filter. |
| Task | string? | Optional task filter. |
| Provider | string? | Optional provider filter. |
| SearchText | string? | Optional name/alias substring. |
| CachedOnly | bool | Restrict to cached models. |

**Behavior**: `Apply(IEnumerable<ModelInfo>) → IReadOnlyList<ModelInfo>` — null criteria are
no-ops (match all); partitions into cached/loaded views are exposed as helpers. Pure and
deterministic.

---

## Entity: RamFitResult  *(pure-logic seam output, FL-free)*

Output of `RamFitHeuristic.Evaluate(sizeGb, freeRamGb)` (FR-016, SC-008; PLAN.md 101).

| Field | Type | Notes |
|---|---|---|
| Fit | enum `Comfortable` \| `Tight` \| `Unlikely` | Size vs **free** RAM with a wide margin. |
| MarginGb | double | Computed headroom used in the decision. |
| LongContextCaveat | bool | Always-on reminder that KV-cache grows with context. |

**Rule (capability honesty)**: never emits a confident "will run" green verdict; expresses
uncertainty. Pure, deterministic, no native call.

---

## Entity: ModelStateGate  *(concurrency primitive, FL-free)* — `Core/Concurrency`

The load/unload concurrency contract (FR-008/009/010; PLAN.md 75). Pure, unit-testable.

**State (per model id)**:

| Field | Type | Notes |
|---|---|---|
| ActiveGenerations | int | Count of in-flight streaming leases on the model. |
| MutationLock | SemaphoreSlim(1,1) | Serializes load/unload on this model. |

**Operations**:
- `BeginGenerationAsync(modelId) → IAsyncDisposable` — increments `ActiveGenerations`; dispose
  decrements. Wrapped around every chat stream.
- `MutateAsync(modelId, Func<Task> op, MutationPolicy policy)` — acquires `MutationLock`
  (serializing concurrent mutations), then:
  - `policy == Drain`: awaits `ActiveGenerations == 0` (bounded) before running `op`.
  - `policy == Reject`: if `ActiveGenerations > 0`, throws `ModelBusyException` (honest,
    actionable; never a fake success / silent no-op — FR-010, SC-004).

**Rules / state transitions**:
- A mutation never runs while `ActiveGenerations > 0` for the **same** model (drains or rejects)
  (FR-008, SC-004).
- Mutations on a **different** model are not blocked (per-model isolation — edge case "active
  stream on a *different* model").
- Concurrent mutations on the same model are serialized by `MutationLock` (FR-009, SC-004).
- Governs requests from both the in-process UI path and the future exposed server — one gate,
  one manager (FR-009, Constitution V).

---

## Entity: FoundryLifecycle (singleton)  *(FL-bound)* — `Foundry`

Owns FL initialization, the ready-gate, and disposal. Implements `IFoundryLifecycle` +
`IAsyncDisposable`. Wraps the **one** `FoundryLocalManager` (Constitution V; FR-003).

| Member | Type | Notes |
|---|---|---|
| _managerTask | `Lazy<Task<FoundryLocalManager>>` | Factory = `Task.Run(InitializeAsync)` — off the dispatcher (KI-005). |
| ReadyAsync(ct) | `Task` | Every consumer awaits this; satisfied **exactly once** on successful init. |
| GetManagerAsync(ct) | `Task<FoundryLocalManager>` | Returns the single shared instance. |
| DisposeAsync() | `ValueTask` | Disposes the manager on app exit (FR-007). |

**Rules / state machine**: `Uninitialized → Initializing` (first awaiter triggers the `Lazy`
factory) `→ Ready` (success; all awaiters observe the same instance — FR-003, SC-002) **or**
`→ Failed` (init threw; ready-gate is **never** satisfied by a failed init — edge case; SC-001).
Hard invariant: **no `.Result`/`.Wait()`** on the init task anywhere (FR-006, SC-003). A second
manager is never constructed (FR-003, SC-002).

---

## Entity: ChatRequest / ChatStreamUpdate  *(adapter surface, via MEAI)* — `Foundry`

The in-process chat surface (FR-012). Uses Microsoft.Extensions.AI `ChatMessage` /
`ChatResponseUpdate` (not redefined here); the adapter (`FoundryChatClient : IChatClient`) maps
them to/from the FL request message type and streams via `IModel.GetChatClientAsync()` →
`CompleteChatStreamingAsync`.

**Rules**: served fully in-process — **no loopback HTTP socket** (FR-012, SC-006); presents a
conventional `IChatClient` so MEAI middleware (`UseFunctionInvocation`, `UseOpenTelemetry`)
composes around it; each stream holds a `ModelStateGate` generation lease (R2/R3).
**Capability honesty**: `response_format` is best-effort only — no "guaranteed JSON" surface
(E4 / FR-018, SC-010).

---

## Entity: PostV1 service stubs  *(FL-bound stubs)* — `Foundry/PostV1`

`IEmbeddingService`, `ITranscriptionService`, `ILocalServerService` defined in Core; stub impls
signal not-implemented honestly.

| Field | Type | Notes |
|---|---|---|
| IsSupported | bool | `false` in v1. |
| (operations) | — | Throw `NotSupportedException("Not implemented in v1")` rather than faking (FR-013). |

**Rule**: never returns fake/empty data to imply support (Constitution IV; FR-013; edge case).

---

## Entity: PinnedVersionSet  *(build/CI gate input — reference)*

The frozen package/SDK set the clean-checkout CI build is gated against (carried from M0;
`Directory.Packages.props` / `KNOWN-GOOD-VERSIONS.md`). Not new app data — referenced here
because FR-017/SC-009 gate on it.

| Field | Type | Notes |
|---|---|---|
| appkit_build | string | `Microsoft.Maui.Platforms.MacOS{,.BlazorWebView,.Essentials}` 0.1.0-preview.8.26256.5. |
| maui_controls / webview_maui | string | 10.0.41 / 10.0.1. |
| foundry_local | string | `Microsoft.AI.Foundry.Local` 1.2.3 (sdk line). |
| meai | string | Microsoft.Extensions.AI 10.0.1. |
| devflow | string | `Microsoft.Maui.DevFlow.{Agent,Blazor}` 0.1.0-preview.8.26256.5 (Debug-only). |
| track | enum | `net10-baseline` (net11 = open chore T004). |

**Validation**: every entry concrete (no "latest"/floating range); CI restore fails if any
dependency resolves off this set (FR-017, SC-009).

---

## Relationships (summary)

```text
FoundryLifecycle (1 singleton) ── wraps ──> FoundryLocalManager (FL, 1)
        ▲ awaited by
        ├── FoundryCatalogService ── maps FL IModel ──> ModelInfo/ModelVariant
        │        └── load/unload ──routes through──> ModelStateGate (per-model)
        │        └── filtering ──uses──> CatalogFilter, RamFitHeuristic (pure seams)
        ├── ChatService ──> FoundryChatClient (IChatClient, in-process, no socket)
        │        └── each stream holds a ──> ModelStateGate generation lease
        ├── PreferencesSettingsService ──persists──> AppSettings (protected user data)
        └── Stub{Embedding,Transcription,LocalServer}Service (honest not-implemented)

FoundryForge.Tests ── references ──> FoundryForge.Core ONLY
        └── covers: SettingsDocument, CatalogFilter, RamFitHeuristic, ModelStateGate
            (all FL-free → no native dylib needed)
```
