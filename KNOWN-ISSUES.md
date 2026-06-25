# KNOWN-ISSUES.md

Running log of workarounds for upstream gaps/bugs. **Every workaround in the codebase has an entry here**, links its tracking issue, and is removed when the upstream fix lands. Policy: never block — workaround, file upstream, remove on fix (see `AGENTS.md` and `docs/PLAN.md`).

Ownership routing:
- **David-owned** (maui-labs AppKit, Comet, DevFlow, maui CLI, Maui.Essentials.AI): fix directly in `~/work/maui-labs`, dogfood via `~/work/LocalNuGets/`.
- **Foundry Local** (`microsoft/Foundry-Local`): local workaround + file issue, route to Maanav Dalal (IC) / Meng Tang (portfolio).
- **Other** (dotnet/maui, dotnet/macios, MEAI, etc.): shim + pin known-good + file upstream.

## Template

```
### KI-NNN — <short title>
- Area: <FL SDK | maui-labs AppKit | dotnet/macios | BlazorWebView | other>
- Symptom: <what breaks>
- Workaround: <what we did, where in the code>
- Upstream: <issue URL or "not yet filed">
- Remove when: <condition>
- Status: <open | upstream-fixed-pending-removal | closed>
```

## Open issues

_None yet. M0 will likely produce the first entries (FL dylib-chain bundling, library-validation entitlement, net11 AppKit package pin)._
