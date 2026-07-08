# Specification Quality Checklist: M5 — Local server toggle (the v1 "wow")

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-06-25
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- This is a milestone spec in an established, technically-grounded repo (FoundryForge). Consistent with the existing M1–M4 specs in `specs/`, it deliberately cites the confirmed Foundry Local SDK seam (`ILocalServerService`, `FoundryLocalManager.StartWebServiceAsync`/`StopWebServiceAsync`/`Urls`), the M1 concurrency primitives (`IModelStateGate`, `FoundryLifecycle.ReadyAsync()`), and KI-001/KI-005 because they are binding ground-truth constraints (Constitution I "Citation Before Action") rather than premature implementation choices. The *user-facing behavior* (toggle, honest endpoint, copy, routes, limitations, external-tool proof) remains the focus; the named seams scope the work honestly per the project's constitution.
- **Honesty acceptance criteria are emphasized throughout**: actual bound URL (not a fake port — FR-007/FR-008, SC-001/SC-002); limitations surfaced not faked (FR-019/FR-020, SC-008); no fabricated request log (FR-022/FR-023, SC-009); server independent of in-app chat (FR-021, SC-005).
- **External-curl success criterion** is explicit: SC-004 and FR-028/SC-011.
- **Concurrency safety** with the shared singleton manager is a P1 user story (US5), FR-015–FR-018, and SC-007.
- No [NEEDS CLARIFICATION] markers: all open choices resolved via reasonable defaults documented in Assumptions; the one plan-vs-reality tension (PLAN "configurable port" vs the no-port FL API) is reconciled explicitly in the Clarifications note per Constitution III/IV.
