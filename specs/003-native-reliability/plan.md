# Implementation Plan
Created: 2026-10-30T06:20:36Z
Owner: Alexander Bleissem
Status: in_progress (worker coverage gap closed; independent published capacity failure remains under investigation)
SDK 10.0.403, C#14/.NET10; Ubuntu24.04 and native Windows CI, original Win11 release prerequisite unchanged. Six unpublished 0.2.0-alpha.1 packages; no dependency upgrades.

## Constitution check
Current code deep dive recorded in analysis.md. Deterministic regressions take precedence over judgment reviews. Async public API, one synchronous store owner, no sync-over-async or automatic replay. Preserve transaction certainty, generic core, all native product libraries and 87% coverage. No baseline or exclusions added.
## Sequence
Architecture gate: apply architecture-decision.md and both opposed reviews. The original PublicError/codec and ProbeExecution proposals are rejected; exception-local details and existing concrete owners replace them. Tests must use the actual host assembly and native target where applicable.
After native CI variability, wave-budget-decision.md records the further opposed
review and explicitly separates a finite Wave acceptance profile from unchanged
SDK/default deadlines. The failed default-budget run remains evidence and has an
owned investigation plan; the changed profile must pass fresh native CI.
1. Outside-repository starvation measurement; validate hypothesis and record limits.
2. Executable regression (real Red), then executor change; capacity/cancellation/disposal regressions.
3. Public safe details and real trigger/commit/timeout Red, then normalization propagation.
4. Preserve diagnostics in validation reports, with sanitization/error-precedence tests.
5. Full local/native CI, static/Cover, package consumption and repeat native scenarios; root-cause claims limited to evidence.
## Validation
Relevant full suites must be green before any refactoring. Archive every pre-Green candidate. Follow any unresolved actionable finding with an owned concrete plan before closing.

Closeout: closeout.md binds both native CI runs, complete coverage/consumption/
published proofs and every finding disposition. The default-budget variability
has an owned ready follow-up; the historic Windows cause and actual Windows11
release acceptance are not closed by this bounded change.
