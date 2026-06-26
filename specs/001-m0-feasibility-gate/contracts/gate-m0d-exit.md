# Gate Contract — M0d: Vertical Slice + Server Capability Check

**Gate**: M0d · **Blocking**: yes (M0 completes when this passes) · **Maps to**: User Story 4, FR-010, FR-011, SC-004, SC-005

## Preconditions

- M0a `passed`, M0b `passed`, M0c `complete`.
- The proven dylib-bundling target and entitlements from M0b are applied to the slice app.

## Inputs

- An AppKit + BlazorWebView app at `spikes/m0d-vertical-slice/` using the **in-process** path (thin `IChatClient` adapter over the FL SDK), mirroring the intended M1 architecture.
- `curl` (or equivalent external client) for the server capability check.

## Exit criteria (ALL must hold to pass)

1. In-process `CreateAsync` initializes Foundry Local on app launch (async-ready, **no `.Result`/`.Wait()`**).
2. The model catalog is listed in the UI (`ICatalog`).
3. The designated test model `qwen2.5-0.5b` downloads and loads into the FL model cache.
4. A single prompt produces a reply that **streams incrementally** into a Razor component (token-by-token render observed).
5. The exposed local server is started; a **tool-calling** request and a **`response_format`** (structured-output) request are sent via external `curl`; each result is recorded as `yes`/`no`/`partial`.
6. The v1 scope decision for tool-calling and structured output is recorded (keep or descope) — honestly, with no fake toggle for an unsupported capability.
7. A `Verified:` line names the real-hardware end-to-end check (download → load → streamed reply; server → external `curl`).

## No-go condition

- The slice cannot initialize, list, load, or stream in-process → declare M0d **no-go** with evidence and rationale in `.squad/decisions.md`; halt before M1.
- A `no` on the server capability check is **not** an M0d failure — it is a recorded scope-bounding result that descopes the matching v1 capability.

## Evidence to capture

- DevFlow log / screen recording of catalog → load → streamed reply on real hardware.
- `curl` request/response transcripts for the tool-calling and `response_format` probes.
- The decision-log entries: M0 overall go, plus the v1 tool/structured-output scope decision.
