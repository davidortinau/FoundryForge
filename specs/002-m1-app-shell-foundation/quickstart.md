# Quickstart: M1 — App Shell + Foundry Local Service Layer + DI + Test/CI Seam

This is the **validation / run guide** that proves the M1 foundation works end-to-end. It is not
implementation code — service/model bodies and full test suites belong to `/speckit.tasks` and
the implementation phase. Each scenario maps to the spec's success criteria.

## Prerequisites

- **Apple Silicon Mac** (macOS 14+). FoundryStudio is macOS / Apple-Silicon only; the FL native
  dylib chain loads on the AppKit head (DEC-004).
- **.NET 10 SDK** matching the M0 baseline (`net10.0-macos`). The net11 pin is the separate open
  chore **T004** and is not required for M1.
- Pinned packages restore from public nuget.org via the central
  [`Directory.Packages.props`](../../Directory.Packages.props) (source of truth:
  [`KNOWN-GOOD-VERSIONS.md`](../../KNOWN-GOOD-VERSIONS.md)). Do not float to "latest."
- Reused M0 build assets (unchanged): [`build/BundleFoundryLocalNative.targets`](../../build/BundleFoundryLocalNative.targets)
  (imported only by the macOS head) and [`Entitlements.Debug.plist`](../../Entitlements.Debug.plist)
  (no `disable-library-validation` needed — M0b/M0d).

## Solution layout (built in this milestone)

See [plan.md → Project Structure](./plan.md#project-structure). Four projects:
`src/FoundryStudio.Core` (FL-free seams + interfaces), `src/FoundryStudio.Foundry` (FL impls +
in-process `IChatClient` adapter + stubs), `src/FoundryStudio.App` (`net10.0-macos` AppKit +
Blazor Hybrid head), `tests/FoundryStudio.Tests` (xUnit, references **Core only**). The M0
spikes under `spikes/` are deleted/archived once the real solution is scaffolded (research R11).

---

## Scenario 1 — Build the solution clean on pinned versions  *(SC-009 / FR-017)*

```bash
cd ~/work/FoundryStudio
dotnet restore FoundryStudio.sln            # resolves ONLY the pinned set; fails on float
dotnet build  FoundryStudio.sln -c Debug    # builds Core, Foundry, App (macOS head), Tests
```

**Expected**: clean restore + build using only pinned versions; the macOS head bundles the FL
dylib chain into `…/FoundryStudio.App.app/Contents/MonoBundle/runtimes/osx-arm64/native/`.
A dependency resolving off the pinned set **fails** the restore (the CI guardrail).

## Scenario 2 — Pure-logic seam tests pass with no native dylib  *(SC-008 / FR-016)*

```bash
dotnet test tests/FoundryStudio.Tests -c Debug
```

**Expected**: green. Covers `SettingsDocument` (defaults / round-trip / never-wipe-without-
consent), `CatalogFilter`, `RamFitHeuristic`, and `ModelStateGate` (drains/rejects, serialization,
per-model isolation). These reference **only** `FoundryStudio.Core`, so they run with **no
Foundry Local dylib present** (the bundle target runs only for the `-macos` head). See
[contracts/IModelStateGate.md](./contracts/IModelStateGate.md),
[contracts/ISettingsService.md](./contracts/ISettingsService.md).

## Scenario 3 — App launches and reaches "ready" without deadlock  *(SC-001, SC-002, SC-003 / FR-003–007)*

Per KI-004, run the inner Mach-O binary directly (not via `open`) so the AppKit head + DevFlow
agent persist:

```bash
dotnet build src/FoundryStudio.App -c Debug
"$(find src/FoundryStudio.App/bin/Debug -name 'FoundryStudio.App' -path '*/MacOS/*' | head -1)"
# then attach DevFlow to confirm the live DOM state:
maui devflow wait
maui devflow webview source           # inspect rendered state (KI-001: screenshot won't capture WKWebView)
```

**Expected**: window shows the **"initializing"** state (chat surface blocked), then transitions
to **"ready"** once the ready-gate is satisfied — **no UI freeze / deadlock** (no KI-005
reproduction). Init ran off the dispatcher (`Task.Run`); UI updated via
`InvokeAsync(StateHasChanged)`. Verify via DOM inspection (KI-001 means `ui screenshot` cannot
capture the WebView content). See [contracts/IFoundryLifecycle.md](./contracts/IFoundryLifecycle.md).

## Scenario 4 — Concurrency-gate service smoke (drains/rejects)  *(SC-004 / FR-008–010)*

Exercised through the service layer (no UI) — either as an xUnit case over `ModelStateGate` or a
small harness:

1. Acquire a generation lease for model `A` (`BeginGenerationAsync("A")`).
2. Request `MutateAsync("A", unloadOp, MutationPolicy.Reject)` → **`ModelBusyException`** (honest,
   not a fake success).
3. Request `MutateAsync("A", unloadOp, MutationPolicy.Drain)` → **waits** until the lease is
   disposed, then runs.
4. Fire two concurrent `MutateAsync("A", …)` → **serialized** (0 concurrent mutations observed).
5. With `A` leased, `MutateAsync("B", …)` → **proceeds** (per-model isolation).

**Expected**: model state is never mutated mid-stream on the same model; rejections are typed
and actionable. See [contracts/IModelStateGate.md](./contracts/IModelStateGate.md).

## Scenario 5 — In-process chat completion, no loopback socket  *(SC-006 / FR-012)*

Through the `IChatService` / `FoundryChatClient` adapter against a loaded model (e.g.
`qwen2.5-0.5b`, as proven in the M0d slice), stream one reply. While streaming, confirm **no**
`127.0.0.1` socket is bound for the chat path:

```bash
lsof -p <app-pid> -iTCP -sTCP:LISTEN     # expect: none from the in-process chat path
```

**Expected**: a streamed reply served fully in-process; structured output is treated as
**best-effort only** (no "guaranteed JSON") per the M0d server finding. See
[contracts/IChatService.md](./contracts/IChatService.md).

## Scenario 6 — Settings persist and are never silently wiped  *(SC-007 / FR-014–015)*

Through `ISettingsService` (no UI): write cache directory, default model, theme; restart the
process; read them back unchanged. Attempt a destructive reset without consent → no-op; inspect
the on-disk JSON → human-readable/editable. (Unit-covered in Scenario 2.) See
[contracts/ISettingsService.md](./contracts/ISettingsService.md).

## Scenario 7 — Capability honesty  *(SC-010 / FR-018)*

Confirm no UI/toggle exists for any unsupported FL capability (server auth/LAN bind, GGUF import,
`top_k`/`min_p`/`seed`, speculative decoding), and that structured output is represented as
best-effort-only. Post-v1 stubs (`IEmbeddingService`, `ITranscriptionService`,
`ILocalServerService`) report `IsSupported == false` and **throw** rather than fake. See
[contracts/IPostV1Services.md](./contracts/IPostV1Services.md).

---

## Milestone close — Verification (Constitution II, NON-NEGOTIABLE) — *FR-021 / SC-011 / SC-012*

M1 is **not done** until all of the following pass and the closing note ends with a `Verified:`
line. The change set must be approved by **someone other than its author** (FR-022, reviewer
independence).

- [ ] **CI clean on pinned versions** — clean-checkout restore + build + test green; fails if any
      dependency floats off the pinned set (Scenario 1 + 2).
- [ ] **xUnit green** — pure-logic seam tests pass with no native dylib (Scenario 2).
- [ ] **Real Apple-Silicon launch-to-ready** — app reaches ready without deadlock, verified via
      DevFlow DOM inspection (Scenario 3; KI-001/KI-004 noted).
- [ ] **Concurrency-gate service smoke** — drains/rejects/serializes/per-model isolation
      (Scenario 4).
- [ ] **In-process chat, no socket** — one streamed reply, zero loopback sockets (Scenario 5).
- [ ] **KI-005 + KI-006 codified** — off-dispatcher init + no `.Result`/`.Wait()`; full
      `_Imports` using-set; any new M1 workaround has a `KNOWN-ISSUES.md` tracking entry (SC-012).

**Verified:** _(filled at close, e.g.)_ `Verified: M1 on Apple Silicon (M-series, macOS 14.x) —
clean-checkout CI restore+build+test green on pinned net10 set; xUnit seam tests green with no FL
dylib; app launched → "initializing" → "ready" with no deadlock (DevFlow DOM-confirmed);
concurrency gate drained/rejected/serialized correctly; one streamed in-process reply with 0
loopback sockets. Reviewed and approved by <reviewer> (not the author).`
