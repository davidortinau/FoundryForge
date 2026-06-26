# Quickstart: Validating the M0 Feasibility Gate

A run guide for executing and verifying M0 end-to-end on a real Apple Silicon Mac. Each gate is go/no-go and fails cheap; do **not** start a gate until the prior blocking gate is `passed`. Full pass conditions live in [`contracts/`](./contracts/); the evidence model is in [`data-model.md`](./data-model.md); rationale is in [`research.md`](./research.md).

## Prerequisites

- Apple Silicon Mac, macOS 14+.
- .NET 11 preview toolchain staged via the `macos-maui-dogfood` skill (pinned `global.json` / `NuGet.config` / `Directory.Packages.props`).
- MAUI DevFlow available (`maui devflow`) for the real-hardware checks.
- Network for first-run model download (offline after).
- Reference: [Redth/MAUI.Sherpa](https://github.com/Redth/MAUI.Sherpa) `src/MauiSherpa.MacOS` for the bundling target + entitlements pattern.

## Gate M0a — Toolchain + version pin

1. Stage the pinned net11 set; ensure no floating versions remain.
2. Build + run the empty baseline app:
   ```bash
   dotnet build spikes/m0a-baseline-app -t:Run
   maui devflow wait
   ```
3. Confirm a window opens and a trivial Blazor page renders.
4. Record the exact versions in `KNOWN-GOOD-VERSIONS.md` (`track: net11-primary`).

**Pass** = [`contracts/gate-m0a-exit.md`](./contracts/gate-m0a-exit.md) criteria met + `Verified:` line. **Fallback** = re-run on the net10 Sherpa set and record the decision.

## Gate M0b — Foundry Local native-load spike (the true go/no-go)

1. In `spikes/m0b-fl-console/` (console head, no MAUI/Blazor), reference the FL SDK (confirm `sdk` vs `sdk_v2`, pin it).
2. Apply `build/BundleFoundryLocalNative.targets`; build with a binlog:
   ```bash
   dotnet build spikes/m0b-fl-console -bl:m0b.binlog
   ```
3. Verify the chain bundled and resolves:
   ```bash
   find spikes/m0b-fl-console -name '*.app' -prune -exec sh -c 'find "$1" -name "*.dylib"' _ {} \;
   # otool -L each bundled dylib; confirm no unresolved @rpath
   ```
   Use `mcp-binlog-tool` on `m0b.binlog` to confirm the copy target fired.
4. Run `CreateAsync` + one inference. If library validation blocks load, add `com.apple.security.cs.disable-library-validation`, re-sign nested dylibs, record the entitlement.

**Pass** = [`contracts/gate-m0b-exit.md`](./contracts/gate-m0b-exit.md) criteria met + `Verified:` line. **No-go** = halt, record evidence, file an FL issue (Maanav Dalal / Meng Tang).

## Gate M0c — BlazorWebView capability probe (informational)

1. In the running app, attempt a local file into `<input type=file>`; record accessible vs blocked.
2. Edit a UI element while running; record whether Hot Reload reflects it.
3. If file intake is blocked, flag the post-v1 native file-intake shim in `.squad/decisions.md`.

**Complete** = [`contracts/gate-m0c-exit.md`](./contracts/gate-m0c-exit.md) findings recorded. Does **not** halt M0.

## Gate M0d — Vertical slice + server capability check

1. Run the slice app (in-process `IChatClient` adapter):
   ```bash
   dotnet build spikes/m0d-vertical-slice -t:Run
   maui devflow wait
   ```
2. Confirm: catalog lists → `qwen2.5-0.5b` downloads + loads → one reply streams incrementally into a Razor component.
3. Start the exposed local server; probe it externally:
   ```bash
   curl -s http://localhost:<port>/v1/chat/completions -d '{ ...tools... }'
   curl -s http://localhost:<port>/v1/chat/completions -d '{ ...response_format... }'
   ```
   Record whether `tools` and `response_format` are honored.
4. Record the M0 overall go and the v1 tool/structured-output scope decision in `.squad/decisions.md`.

**Pass** = [`contracts/gate-m0d-exit.md`](./contracts/gate-m0d-exit.md) criteria met + `Verified:` line.

## Definition of done for M0

- M0a `passed`, M0b `passed`, M0c `complete`, M0d `passed`.
- `KNOWN-GOOD-VERSIONS.md` pins the proven set; `KNOWN-ISSUES.md` links any workaround.
- `.squad/decisions.md` records every go/no-go + the v1 scope decision.
- Each gate has a `Verified:` line from a real Apple-Silicon end-to-end check.
- Only then does M1 (app shell + service layer) begin.
