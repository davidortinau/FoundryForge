# Gate Contract — M0a: Toolchain + Version Pin

**Gate**: M0a · **Blocking**: yes · **Maps to**: User Story 1, FR-003, FR-004, SC-001

The "contract" of a feasibility gate is its **exit criteria**: the exact conditions that must be true to mark it `passed`, and what a `no-go` looks like.

## Preconditions

- The pinned version set is staged via the `macos-maui-dogfood` skill (net11 primary).
- `Directory.Packages.props`, `global.json`, `NuGet.config` reflect concrete, non-floating versions.

## Inputs

- An empty, Sherpa-shaped AppKit + BlazorWebView app at `spikes/m0a-baseline-app/`.

## Exit criteria (ALL must hold to pass)

1. The app restores and builds from a clean checkout using **only** the pinned versions (no floating ranges, no implicit "latest").
2. The app launches on an Apple Silicon Mac and a native window opens.
3. A trivial Blazor page renders inside the BlazorWebView.
4. The exact proven version set is recorded in `KNOWN-GOOD-VERSIONS.md` with `track` = `net11-primary`.
5. A `Verified:` line names the real-hardware launch check.

## No-go condition

- The net11 AppKit set fails to build or launch after a bounded attempt → fall back to the net10 Sherpa reference set, re-run criteria 1–5 with `track` = `net10-fallback`, and record the fallback decision in `.squad/decisions.md`. (A true project halt only occurs if **neither** track builds + launches, which Sherpa makes highly unlikely.)

## Evidence to capture

- Build output / binlog showing the pinned versions resolved.
- Screenshot or DevFlow log of the launched window with the rendered page.
- The `KNOWN-GOOD-VERSIONS.md` diff pinning the set.
