# Contract: Chat UI DOM surface (DevFlow verification hooks)

M4 activates the sidebar **Chat** nav and adds the chat surface under `src/FoundryStudio.App/Components/Pages/Chat.razor` + `…/Components/Chat/*`. It exposes stable `id` / `data-testid` attributes so M4 is verified via DevFlow **DOM inspection** (KI-001 path: `webview source` / `Runtime evaluate`) without relying on `ui screenshot`. This contract is what the DevFlow e2e and any future UI tests assert against, and it encodes the **honesty** (Constitution III) and **consent** (Constitution IV) invariants. The M3 hooks (`specs/004…/contracts/management-ui.dom.md`) remain valid; M4 **flips** the M3 negative invariant that inference-param controls are absent — they are now intentionally present **on the chat surface only**, and only the four real ones.

## Navigation (US1 / FR-001)

| Element | id / data-testid | Notes |
|---------|------------------|-------|
| Chat nav link | `id="nav-chat"` `data-testid="nav-chat"` | now an **enabled** `NavLink href="/chat"` — **no** `disabled`/`aria-disabled`/"Coming soon" (M3 placeholder removed) |

## Chat surface root + gate (US1/US2)

| Element | id / data-testid | Notes |
|---------|------------------|-------|
| Chat page root | `data-testid="chat-page"` | route `/chat` |
| Active-model name | `data-testid="chat-active-model"` `aria-live="polite"` | names the loaded model when ready |
| No-model gate | `data-testid="chat-gate"` | honest "a model must be loaded"; shown when none loaded (FR-007) |
| Load-on-demand button | `data-testid="chat-gate-load"` | `LoadAsync(alias, …)` via the M1 gate (FR-008) |
| Busy/not-ready message | `data-testid="chat-busy"` `role="alert"` | honest `ModelBusyException` / unloaded-elsewhere state; no hang (FR-009/010) |

## Composer + streaming (US1/US3)

| Element | id / data-testid | Wired to | Story/FR |
|---------|------------------|----------|----------|
| Composer input | `data-testid="chat-composer"` + `<label>` | placeholder **"Ask this local model…"** (DESIGN §9) | US1 / FR-006 |
| Send button | `data-testid="chat-send"` | `StreamAsync(...)`; **Foundry Copper** accent (DESIGN §8); disabled on empty/whitespace | US1 / FR-006 |
| Stop button | `data-testid="chat-stop"` | `CancellationTokenSource.Cancel()`; **present only while streaming** (FR-013) | US3 / FR-011 |
| Message list | `data-testid="chat-messages"` | ordered turns; multi-turn context | US1 / FR-004 |
| User turn | `data-testid="chat-msg-user"` | — | US1 |
| Assistant turn | `data-testid="chat-msg-assistant"` + `data-streaming="true|false"` | renders **incrementally** from real updates (multiple partial renders) | US1 / FR-003 |
| Awaiting-first-token state | `data-testid="chat-awaiting"` | honest "pouring response" / awaiting state; **no fabricated partial text** (FR-006) | US1 / FR-006 |
| Stream error | `data-testid="chat-error"` `role="alert"` | honest diagnosed cause; partial text preserved; returns to ready | edge / FR-035 |

## Markdown + code copy (US1 / FR-005)

| Element | data-testid | Notes |
|---------|-------------|-------|
| Rendered markdown | `data-testid="chat-markdown"` | headings/lists/emphasis/inline code via `ChatMarkdown` (HTML sanitized — `DisableHtml`) |
| Code block | `data-testid="chat-codeblock"` | distinct fenced block |
| Copy code button | `data-testid="chat-code-copy"` + `aria-label` | copies the **exact** raw code (from `RenderedMarkdown.CodeBlocks`) |

## Metrics row (US4 / FR-014) — mono, tertiary (DESIGN §8)

| Element | data-testid | Notes |
|---------|-------------|-------|
| Metrics row | `data-testid="chat-metrics"` | mono tertiary; tokens/sec · token count · time · stop reason |
| TTFT | `data-testid="metric-ttft"` | measured from send to first real update |
| Tokens/sec | `data-testid="metric-tps"` | observed tokens ÷ elapsed |
| Total tokens | `data-testid="metric-total"` | real usage **or** honest "unknown" (FR-016) |
| Stop reason | `data-testid="metric-stop"` | natural/max-tokens/user-cancelled/error/unknown (FR-015) |

## Context-window tracker (US6 / FR-020) — explicit estimate

| Element | data-testid | Notes |
|---------|-------------|-------|
| Context meter | `data-testid="chat-context"` | advances with usage; **labeled an estimate** in all states (FR-020) |
| Context warning | `data-testid="chat-context-warn"` `role="status"` | appears near the limit; "older turns may fall out of context" (FR-021) |
| Unknown-context state | `data-testid="chat-context-unknown"` | honest "limit unknown" — **no fabricated denominator/%** (FR-022) |

## System prompt + the FOUR real params (US5 / FR-018)

| Element | id / data-testid | Notes |
|---------|------------------|-------|
| System-prompt editor | `data-testid="chat-system-prompt"` + `<label>` | applied as system message; persists (FR-017) |
| Temperature | `data-testid="param-temperature"` + `<label>` | maps to `ChatOptions.Temperature` |
| Max tokens | `data-testid="param-max-tokens"` + `<label>` | maps to `ChatOptions.MaxOutputTokens` |
| Top-p | `data-testid="param-top-p"` + `<label>` | maps to `ChatOptions.TopP` |
| Frequency penalty | `data-testid="param-frequency-penalty"` + `<label>` | maps to `ChatOptions.FrequencyPenalty` |

## Conversation list + history actions (US7 / FR-023)

| Element | id / data-testid | Wired to | FR |
|---------|------------------|----------|----|
| Conversation list | `data-testid="chat-conversations"` | `IChatHistoryStore.ListAsync()` (reloads on restart) | FR-023 |
| New chat | `data-testid="chat-new"` | non-destructive create + `SaveAsync` | FR-024 |
| Duplicate chat | `data-testid="chat-duplicate"` | non-destructive copy of source | FR-024 |
| Clear messages | `data-testid="chat-clear"` | **opens** `ConfirmDialog` — does NOT clear | FR-025 |
| Delete chat | `data-testid="chat-delete"` | **opens** `ConfirmDialog` — does NOT delete | FR-025 |

## Consent dialog (reused `ConfirmDialog.razor` — US7 / Constitution IV)

| Element | data-testid | Notes |
|---------|-------------|-------|
| Dialog root | `data-testid="confirm-dialog"` `role="dialog"` `aria-modal="true"` | hidden until activated |
| Dialog message | `data-testid="confirm-message"` | **names the exact conversation** + states it **cannot be undone** (FR-025) |
| Confirm button | `data-testid="confirm-accept"` | calls `DeleteAsync(id, userConfirmed:true)` / `ClearMessagesAsync(id, true)` (FR-026) |
| Cancel button | `data-testid="confirm-cancel"` `data-autofocus="true"` | **default-focused**; no-op; deletes nothing (FR-025/026) |

## Tools (US8 / FR-030) — genuine, only when a tool runs

| Element | data-testid | Notes |
|---------|-------------|-------|
| Tool activity | `data-testid="chat-tool-activity"` | shown **only** when a real tool executed (e.g. `get_current_time`/`get_loaded_model_info`); reflects a genuine invocation |
| Tool error | `data-testid="chat-tool-error"` `role="alert"` | surfaced plainly, not hidden/faked (FR-029) |

## Structured output / regenerate (US9 OPTIONAL P3 — only if implemented)

| Element | data-testid | Notes |
|---------|-------------|-------|
| Structured-output affordance | `data-testid="chat-structured"` | if present, labeled **best-effort only**; **no** "guaranteed/enforced JSON" claim or enforcement toggle (FR-031) — preferably omitted |
| Regenerate last | `data-testid="chat-regenerate"` | if present, re-streams for the same prior context, replaces the last assistant turn (FR-032) |

## Honesty & consent negative invariants (verified — Constitution III/IV)

On real Apple Silicon, the DOM MUST satisfy:
- **Zero** controls for unsupported params anywhere — these selectors MUST be **absent**:
  ```
  [data-testid="param-top-k"], [data-testid="param-min-p"], [data-testid="param-repeat-penalty"], [data-testid="param-seed"]
  ```
- **Exactly four** param controls present (`param-temperature`, `param-max-tokens`, `param-top-p`, `param-frequency-penalty`) (FR-019, SC-006).
- A single activation of `chat-delete`/`chat-clear` produces a `confirm-dialog` and performs **zero** removals; `confirm-cancel` removes nothing (FR-025/026, SC-009).
- **No** one-click destructive delete/clear path (nothing removes without going through `confirm-accept`).
- `chat-msg-assistant` shows **multiple** successive partial renders during a stream (`data-streaming="true"` → `false`) — never a single post-hoc paste, never a typewriter animation with no stream (FR-003).
- Any genuinely-unavailable metric renders honest "unknown" (`metric-total`), and the context tracker renders "unknown" with **no** fabricated denominator when `ContextLength` is null (FR-016/022).
- **No** "guaranteed JSON"/enforcement toggle exists (FR-031).
- `chat-tool-activity` appears **only** when a real tool executed — no decorative/do-nothing tool affordance (FR-030).

## Accessibility contract (WCAG AA — FR-036, SC-012)
- Every new control (composer/send, Stop, system-prompt + the four params, context tracker, conversation new/duplicate/clear/delete + confirmation, code-copy, tool affordances) has an associated `<label>`/`aria-label`, is keyboard-reachable, and announces state changes.
- The streamed assistant region uses `aria-live="polite"` so incremental text is perceivable.
- The confirm dialog is `role="dialog" aria-modal="true"`, focus-trapped, Esc = cancel, **Cancel default-focused**.
- Ready / streaming / busy / warn states are conveyed by **text + icon**, never color alone (no info by color).
- The "estimate" qualifier on the context tracker is conveyed in text (not by color).
- Contrast meets AA in **both** Workshop Daylight and Night Forge (verified via computed-style query in DevFlow).
