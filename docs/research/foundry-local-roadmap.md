# Microsoft Foundry Local — roadmap & desktop experience (research brief)

*Researched 2026-06-24 for David Ortinau. Sources: live `gh` against `microsoft/Foundry-Local`, Microsoft Learn (architecture + Windows AI docs), NuGet, and internal WorkIQ signal. Cross-validated across two independent research passes. Naming: "Foundry Local" is the on-device runtime under the Microsoft Foundry brand.*

## TL;DR

- **Foundry Local is GA** (v1.0 on 2026-04-09), now at **v1.2.1 (2026-06-05)**, shipping every ~3-4 weeks. The **CLI is still public preview** on a separate track (0.10.1, 2026-06-22).
- **There is no standalone Foundry Local desktop GUI app.** The product *is* an embedded on-device runtime (~20 MB native library) that developers ship inside their own apps. The only first-party "app-like" surface is the **`foundry` CLI** (winget/MSIX). There is an **Electron chat sample** and a SvelteKit marketing site, but no end-user desktop application.
- **The defining roadmap move is architectural:** Foundry Local is shifting from a **separate service/daemon** to an **in-process single native library** (`Core API` = `.dll`/`.so`/`.dylib`) with thin language SDKs. This is the `sdk_v2` **C++ rewrite** in the repo, and it's why a **SDK migration guide** now exists. The new CLI (0.10.x) is already "built on the SDKs, replacing the earlier service-based CLI."
- **Desktop developer story = embed the SDK in any .NET host.** Docs explicitly name **console, WinUI 3, WPF, "or any other .NET host."** As of v1.1.0 the C# SDK also targets **`netstandard2.0`**, which unlocks **.NET MAUI, Xamarin, and Unity** referencing without tricks. **No Android/iOS** — Foundry Local is desktop-only (Windows, macOS Apple Silicon, Linux).

---

## Desktop experience (the core question)

### Is there a desktop app?
No dedicated GUI app. The "desktop experience" is three things:
1. **The embedded runtime/SDK** developers put inside their WinUI 3 / WPF / MAUI / Electron / console apps. This is the product.
2. **The `foundry` CLI** (preview) — an interactive developer tool, installed per-developer via `winget install Microsoft.FoundryLocal` (Windows) or `brew install foundrylocal` (macOS). Not shipped to end users.
3. **Samples** showing GUI integration — notably an **Electron chat app** (`samples/js/electron-chat-application`). There is **no MAUI sample** yet.

### How it runs on the desktop (architecture)
The new model is **in-process, no daemon**: your app loads the **Foundry Local Core API** (a native library) and calls it through a language SDK. End users get a single distributable with no separate installer or background service.

```
Your app  →  Foundry Local SDK (C# / JS / Python / Rust)
          →  Core API (native .dll/.dylib/.so, in-process)
          →  ONNX Runtime  →  Execution Providers
             Windows: WinML → CUDA / Qualcomm QNN / Intel OpenVINO / AMD Vitis / TensorRT-RTX / CPU
             macOS (Apple Silicon): WebGPU → Dawn → Metal + CPU
             Linux: CUDA / WebGPU / CPU
          →  Foundry Catalog (cloud; first-run model + EP download only, then fully offline)
```
- **Windows** is the lead platform: `Microsoft.AI.Foundry.Local.WinML` uses **WinML** to source hardware-matched execution-provider plugins from **Windows Update**, and is positioned as the "go deeper than the Windows AI APIs / support non-Copilot+ PC hardware" tier of **Microsoft Foundry on Windows**.
- **macOS Apple Silicon** is real (WebGPU→Dawn→Metal, FP16). **Linux** supported (CUDA/CPU).
- Hardware auto-selection by **model alias** (e.g. `phi-3.5-mini`): picks QNN NPU on Snapdragon, CUDA on NVIDIA, CPU fallback everywhere.

### The SDK surface (.NET / C#)
Package: **`Microsoft.AI.Foundry.Local`** (cross-platform) and **`Microsoft.AI.Foundry.Local.WinML`** (Windows, hardware-accelerated). NuGet, currently pre-release/floating.

| Class | Purpose |
|---|---|
| `FoundryLocalManager` | Async singleton entry — `CreateAsync()`, `GetCatalogAsync()`, `DiscoverEps()`, `DownloadAndRegisterEpsAsync()` |
| `Configuration` | `AppName` (required), `AppDataDir`, `ModelCacheDir`, `LogLevel`, optional `Web.Urls` |
| `ICatalog` / `IModel` | catalog browse + model lifecycle (`DownloadAsync`, `LoadAsync`, `IsCachedAsync`, `Unload`) |
| `OpenAIChatClient` | chat completions, sync + `IAsyncEnumerable` streaming |
| `OpenAIAudioClient` / `LiveAudioTranscriptionSession` | file + **real-time mic transcription** (added v1.1.0) |
| `OpenAIEmbeddingClient` | on-device **text embeddings** (added v1.1.0) |

Minimal pattern: `CreateAsync` → `GetCatalogAsync` → `GetModelAsync(alias)` → `DownloadAsync`/`LoadAsync` → `GetChatClientAsync` → `CompleteChat[Streaming]Async`. Windows apps target `net9.0-windows10.0.26100.0` and call `DownloadAndRegisterEpsAsync()` once to enable GPU/NPU. An **optional OpenAI-compatible REST endpoint** (`/v1/chat/completions`, `/v1/responses`, `/v1/models`) can be started in-process for LangChain/Open WebUI.

### The CLI (`foundry`) — preview
SDK-based (replaced the old service-based CLI). Surface: `foundry model list [--variants|--filter device=NPU]`, `model run|load|unload|download|info`, `chat`, `transcribe`, `server start|stop|restart|logs`, `status`, `cache rm`, `config show|set|reset`, `report`. Ships as **MSIX with WinML variants** for Win x64/ARM64; macOS/Linux assets too. Still flagged "early, expect rough edges."

---

## Roadmap & plans (what's shipped vs. coming)

**Shipped (GA cadence, last ~3 months):**
- **v1.0 GA** (2026-04-09) → v1.1.0 (May 5) → v1.2.0 (May 28) → **v1.2.1 (Jun 5)**.
- v1.1.0: **`netstandard2.0`/`net8.0` targets** (Xamarin/Unity/MAUI reach), **live audio transcription** (Nemotron ASR, OpenAI Realtime-compatible), **text embeddings** (`qwen3-0.6b-embedding`), **Qwen 3.5 Vision** (multimodal), **WebGPU EP as downloadable plugin**.
- v1.2.x: **BYOM (bring-your-own-model) cache discovery with no service restart**, multilingual ASR, Azure catalog/registry resilience, Windows non-ANSI path fix.

**In flight / planned (strongest signals):**
1. **`sdk_v2` C++ rewrite** — unify all language SDKs (C#/JS/Python/Rust) on one native Core API; the in-process, no-daemon, single-distributable architecture above. Repo has `sdk_v2/` with a "C++ Rewrite Reviewing Guide" + a published **SDK migration guide** on Learn. This is the dominant near-term engineering investment.
2. **CLI to GA** — still preview (0.10.x); internal ADO feature 36969841 "Become part of Foundry Local CLI" is **High priority, active (created Mar 3, touched Jun 24 2026)**.
3. **WinML / Windows AI convergence** — EP acquisition via Windows Update; positioned as the deeper tier beneath Windows AI APIs / Copilot+ PC. Expanding NPU coverage (Qualcomm, AMD Vitis, Intel OpenVINO, TensorRT-RTX).
4. **Modality + catalog expansion** — chat, embeddings, vision, ASR already; custom ONNX via Olive/Hugging Face compile; agent integration via Agent Foundry; OpenAI Responses API surface.

**Internal planning artifacts (not public):** two "Azure AI Foundry Local.pptx" roadmap decks on SharePoint (modified Apr 23 and May 25 2026) — exist but milestones weren't extractable from WorkIQ snippets. **No public ROADMAP.md, CHANGELOG.md, or GitHub milestones** — roadmap is tracked internally (ADO + decks), not in the open repo.

**Ownership (post-reorg):** Foundry Local sits in **Meng Tang's** PM portfolio (reports to **Tina Schuchman**, who now consolidates Foundry PM); **Maanav Dalal** has been the named feature/IC PM. Single-source each — confirm before quoting.

---

## What this means for .NET MAUI (David's angle)

- **MAUI can consume Foundry Local today on desktop heads only:** MAUI-Windows (`Microsoft.AI.Foundry.Local.WinML`, `net9.0-windows10.0.26100.0`) and MAUI-Mac Catalyst / macOS (`Microsoft.AI.Foundry.Local`, Apple Silicon). The `netstandard2.0` target (v1.1.0) removes prior referencing friction.
- **It does NOT cover MAUI's mobile heads** (Android/iOS) — Foundry Local is desktop-only. On-device AI for MAUI mobile still routes through ONNX Runtime GenAI directly, not Foundry Local. This is the key gap to flag in any "AI on the client" narrative.
- **There's no MAUI sample** — a `Microsoft.AI.Foundry.Local` + MAUI (macOS/Windows) Blazor Hybrid sample is an obvious, cheap artifact to land, and the natural follow-through on the stalled blog [dotnet-blog #2219](https://github.com/microsoft/dotnet-blog/pull/2219). Maanav (IC) is the faster contact; Meng (portfolio) the escalation point.

## Sources
- Repo: https://github.com/microsoft/Foundry-Local · releases: https://github.com/microsoft/Foundry-Local/releases · C# SDK: https://github.com/microsoft/Foundry-Local/blob/main/sdk/cs/README.md · `sdk_v2/` (C++ rewrite) and `samples/js/electron-chat-application`
- Architecture: https://learn.microsoft.com/azure/foundry-local/concepts/foundry-local-architecture
- Windows AI get-started (WinUI 3/WPF/console): https://learn.microsoft.com/windows/ai/foundry-local/get-started
- CLI reference: https://learn.microsoft.com/azure/foundry-local/reference/reference-cli · SDK migration guide: https://learn.microsoft.com/azure/foundry-local/reference/reference-sdk-migration
- NuGet: https://www.nuget.org/packages/Microsoft.AI.Foundry.Local · https://www.nuget.org/packages/Microsoft.AI.Foundry.Local.WinML
- Catalog/site: https://foundrylocal.ai · https://www.foundrylocal.ai/models
- Internal (WorkIQ): roadmap decks (SharePoint, Apr 23 / May 25 2026); CLI ADO feature 36969841; reorg email (Meng Tang / Tina Schuchman, Jun 2026)
