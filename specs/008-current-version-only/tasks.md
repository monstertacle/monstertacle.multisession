# Tasks

- [x] T001 Inventory and decisions (research.md; joint contrary review in SharpClaw specs/007).
- [x] T002 Refusal coverage: format1 (user_version 1, SchemaVersion 1) and a non-UTC stored timestamp. No Red: both were already refused by current code (format checks, snapshot consistency against indexed columns); the tests close the coverage gap left by removing the prior-package probe. The converter tolerance is unobservable and stays unchanged.
- [x] T003 Remove probe, migration guide, legacy wording (incl. docs/operations.md migration-plan sentence); converter unchanged (see T002); version 0.2.0-alpha.5; lock files.
- [x] T004 Local Linux quality; push; native CI; canonical alpha.5 feed in evidence/alpha5-feed.json.
- [x] T005 SharpClaw consumes alpha.5.

T005 (2026-11-01): SharpClaw consumes the canonical alpha.5 bytes on its branch feature/007-current-version-only (ef395cf); its regression is recorded there.

- [ ] T006 StopCallbackDeadlineTests.ReportsDeadlineWhileCancellationCallbackIsBlocked timed out once on windows-latest (run 41305730726, first attempt; rerun passed). Owner: integration maintainer. Scope: find whether the deadline or the test wait is too tight under a loaded runner; fix the responsible boundary without widening product limits. Acceptance: cause recorded; 20 consecutive Windows runs of that test class pass in CI.
