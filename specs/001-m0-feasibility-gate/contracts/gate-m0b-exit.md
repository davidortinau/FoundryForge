# Gate Contract — M0b: Foundry Local Native-Load Spike

**Gate**: M0b · **Blocking**: yes (**the project's true go/no-go**) · **Maps to**: User Story 2, FR-005…FR-008, SC-002

## Preconditions

- M0a `passed` (a buildable, launchable pinned toolchain exists).
- Foundry Local package identity (`sdk` vs `sdk_v2`) and exact version confirmed and pinned.

## Inputs

- A minimal macOS **console** head at `spikes/m0b-fl-console/` with the Foundry Local SDK referenced. **No MAUI, no Blazor.**
- `build/BundleFoundryLocalNative.targets` adapted from Sherpa's `_BundleNativeForMacOS`.
- `Entitlements.Debug.plist` (hardened runtime; `network.server`, `cs.allow-jit`).

## Exit criteria (ALL must hold to pass)

1. The complete FL dylib chain (`libfoundry_local` + ONNX Runtime + ONNX Runtime GenAI + Dawn) is present at `Contents/MonoBundle/runtimes/osx-arm64/native/` — verified by `find *.app -name '*.dylib'`.
2. Every inter-library reference in the chain resolves within the bundle — verified by `otool -L` on each dylib (no unresolved `@rpath`).
3. In-process Foundry Local initialization (`CreateAsync`) succeeds without `DllNotFoundException` or a library-validation crash.
4. At least one inference returns a valid completion.
5. If library validation blocked load, `com.apple.security.cs.disable-library-validation` is added, nested dylibs re-signed, and the required entitlement is recorded in `Entitlements.Debug.plist` + `KNOWN-ISSUES.md` (if a workaround) / `KNOWN-GOOD-VERSIONS.md` (FL package pin).
6. A `Verified:` line names the real-hardware load + inference check.

## No-go condition

- After applying the entitlement + nested-dylib re-sign remedy, the chain still fails to bundle, resolve, or load in-process → declare M0b **no-go**, record `otool`/crash evidence and rationale in `.squad/decisions.md`, and **halt** (no M0c/M0d/M1). File a precise FL issue (route: Maanav Dalal / Meng Tang) with a minimal repro and cited source line.

## Evidence to capture

- `find` listing of bundled dylibs; `otool -L` output per dylib.
- `mcp-binlog-tool` analysis of the build binlog showing the copy target firing.
- Console output of `CreateAsync` + the inference result (or the crash log on no-go).
