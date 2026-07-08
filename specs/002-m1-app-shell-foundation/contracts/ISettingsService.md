# Contract: `ISettingsService` (app settings / persistence store)

**Project**: `FoundryForge.Core/Abstractions` (interface) + `Core/Settings/SettingsDocument.cs`
(pure logic) · impl `FoundryForge.Foundry/PreferencesSettingsService.cs`
**Satisfies**: FR-014, FR-015 · SC-007 · Constitution IV · PLAN.md line 95

Persists app settings (model cache directory, default model, theme) across launches, fully
user-editable and auditable, **never wiped or destructively overwritten without explicit
per-action consent**. The cache directory it stores points at multi-GB protected user data.

```csharp
namespace FoundryForge.Core.Abstractions;

using FoundryForge.Core.Models; // AppSettings, AppTheme

public interface ISettingsService
{
    /// Returns persisted settings, or documented DEFAULTS when unset/missing/empty/corrupt
    /// (non-destructive recovery — prior data is preserved, never silently wiped).
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);

    /// Persists a non-destructive update (set/edit individual values).
    Task UpdateAsync(AppSettings settings, CancellationToken cancellationToken = default);

    /// Destructive operations (clear/reset, or changing the cache dir away from existing
    /// multi-GB data) REQUIRE explicit consent; without it the call does not proceed.
    Task ResetAsync(bool userConfirmed, CancellationToken cancellationToken = default);
}
```

Pure logic in `SettingsDocument` (FL-free, unit-tested): JSON (de)serialization,
merge-missing-with-defaults, and the `RequireConsent` guard.

### Behavioral contract

| # | Given | When | Then |
|---|---|---|---|
| 1 | Fresh install (no settings written) | `GetAsync` | returns documented **defaults**, including a default model cache directory (FR-014, SC-007). |
| 2 | User sets cache dir / default model / theme, then restarts | `GetAsync` after restart | the persisted values are read back **unchanged** (100% fidelity — FR-014, SC-007). |
| 3 | Persisted settings exist | a clear/destructive-overwrite is attempted without consent | it does **not** proceed; persisted state is never silently wiped (FR-015, SC-007). |
| 4 | The persistence store | contents inspected | **human-auditable & user-editable** JSON, not opaque (FR-015). |
| 5 | Settings file missing/empty/corrupt | `GetAsync` | documented defaults applied; the prior (unparseable) file is preserved (e.g., `.bak`), not destroyed (edge case). |

### Persistence shape
- JSON document in app data (human-readable, user-editable), anchored/keyed via MAUI Essentials
  `Preferences` (PLAN.md 95).
- `AppSettings`: `ModelCacheDirectory` (string path → protected user data), `DefaultModel`
  (string?), `Theme` (`Light`|`Dark`|`Auto`), `SchemaVersion` (int).

### Test notes (Story 5 independent test — no UI, no dylib)
`SettingsDocumentTests`: defaults returned when unset; round-trip set→read fidelity; corrupt
input → defaults + original preserved; reset-without-consent is a no-op; reset-with-consent
proceeds. All run against pure `Core` (no FL dylib — SC-008).
