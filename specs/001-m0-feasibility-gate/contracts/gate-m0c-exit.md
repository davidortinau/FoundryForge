# Gate Contract — M0c: BlazorWebView Capability Probe

**Gate**: M0c · **Blocking**: no (informational; does not halt M0) · **Maps to**: User Story 3, FR-009, SC-003

## Preconditions

- M0b `passed`.
- A running AppKit + BlazorWebView app (the M0a baseline or the M0d slice) available to probe.

## Inputs

- A page with an `<input type=file>` element and a trivially editable UI element.

## Exit criteria (gate is `complete` when ALL findings are recorded)

1. **File intake**: attempt to bring a local file into the `<input type=file>`; record whether the file's contents are accessible or blocked by the WKWebView sandbox.
2. **Hot Reload**: edit a UI element while the app runs; record whether the change appears without a full rebuild.
3. If file intake is **blocked**, a post-v1 **native file-intake shim** need is flagged in `.squad/decisions.md` (affects post-v1 RAG/voice document intake).
4. A `Verified:` line names the probe run.

## Not a halt condition

- A blocked file intake or non-working Hot Reload does **not** fail M0. Rich Blazor Hybrid UI is already proven by Sherpa; these findings inform post-v1 planning and developer ergonomics only.

## Evidence to capture

- Observation notes (accessible / blocked) for file intake.
- Observation notes (works / requires rebuild) for Hot Reload.
- The decision-log flag, if a shim is needed.
