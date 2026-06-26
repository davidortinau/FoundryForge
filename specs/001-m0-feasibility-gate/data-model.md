# Phase 1 Data Model: M0 Feasibility Gate

M0 produces no application data store. The "data" here is the **evidence and decision record** for each gate — lightweight, human-authored, captured in `.squad/decisions.md` and the version/issue references. This model defines the shape so every gate is recorded consistently.

## Entity: Gate

A single feasibility checkpoint.

| Field | Type | Notes |
|---|---|---|
| id | enum | `M0a` \| `M0b` \| `M0c` \| `M0d` |
| title | string | Short gate name |
| status | enum | `pending` \| `passed` \| `no-go` (M0c may also be `complete` since it is informational) |
| blocking | bool | True for M0a, M0b, M0d; false for M0c |
| evidence | Evidence[] | Captured artifacts proving the outcome |
| decision | DecisionRecord | The recorded go/no-go and rationale |
| verified_line | string | The `Verified:` line from the real-hardware end-to-end check |

**State transitions**: `pending → passed` (evidence satisfies exit contract + Verified line recorded) or `pending → no-go` (exit contract unmet; evidence + rationale recorded). A blocking gate at `no-go` halts all later gates and M1+. M0c at `complete` with a blocked finding does **not** halt; it records a post-v1 flag.

**Sequencing rule**: a gate may enter `pending` only after the prior blocking gate is `passed`. Order: M0a → M0b → M0c → M0d.

## Entity: PinnedVersionSet

The package/SDK set proven during M0a (and FL package in M0b).

| Field | Type | Notes |
|---|---|---|
| dotnet_sdk_band | string | Exact net11 SDK band (or net10 fallback) |
| appkit_build | string | maui-labs `Microsoft.Maui.Platforms.MacOS{,.BlazorWebView,.Essentials}` version |
| maui_controls | string | Matched `Microsoft.Maui.Controls` version |
| webview_maui | string | `Microsoft.AspNetCore.Components.WebView.Maui` version |
| foundry_local_pkg | string | `sdk` vs `sdk_v2` identity + exact version (M0b) |
| track | enum | `net11-primary` \| `net10-fallback` |

**Validation**: every field is concrete (no "latest", no floating range) before M0a is `passed`. Recorded in `Directory.Packages.props` and `KNOWN-GOOD-VERSIONS.md`.

## Entity: NativeDylibChain

The Foundry Local native payload that must bundle and resolve together (M0b).

| Field | Type | Notes |
|---|---|---|
| members | string[] | `libfoundry_local`, ONNX Runtime, ONNX Runtime GenAI, Dawn (confirm exact filenames in M0b) |
| bundled_path | string | `Contents/MonoBundle/runtimes/osx-arm64/native/` |
| rpath_resolved | bool | `otool -L` confirms every inter-library reference resolves in-bundle |
| requires_disable_library_validation | bool | Set by M0b; drives the entitlements plist |

**Validation**: all `members` present at `bundled_path` AND `rpath_resolved == true` AND one in-process inference succeeds → contributes to M0b `passed`.

## Entity: TestModel

The small model used to prove download/load/stream (M0d).

| Field | Type | Notes |
|---|---|---|
| alias | string | `qwen2.5-0.5b` (designated small model) |
| cached | bool | Downloaded to the FL model cache (user data; never wiped) |
| streamed_reply_observed | bool | At least one reply rendered incrementally in a Razor component |

## Entity: ServerCapabilityFinding

The M0d go/no-go input for v1 scope.

| Field | Type | Notes |
|---|---|---|
| tools_honored | enum | `yes` \| `no` \| `partial` |
| response_format_honored | enum | `yes` \| `no` \| `partial` |
| v1_scope_decision | string | Keep or descope tool-calling / structured output in v1 |

**Rule (capability honesty)**: a `no` finding descopes the matching v1 feature; it is never papered over with a fake toggle.

## Entity: DecisionRecord

Appended to `.squad/decisions.md` per gate.

| Field | Type | Notes |
|---|---|---|
| date | date | |
| gate | enum | Which gate produced it |
| outcome | enum | `go` \| `no-go` |
| rationale | string | Cited evidence behind the call |
| follow_ups | string[] | e.g., version pin recorded, KNOWN-ISSUES link, scope change |

## Entity: WorkaroundRecord

For any upstream gap mitigated during M0 (FR-015).

| Field | Type | Notes |
|---|---|---|
| description | string | The workaround applied |
| upstream | enum | `foundry-local` \| `maui-labs` \| `other` |
| tracking_link | string | Issue URL recorded in `KNOWN-ISSUES.md` |
| remove_when | string | Condition under which the workaround is deleted |

**Rule**: every workaround has a `tracking_link` before its gate is `passed`; removed when the upstream fix lands.
