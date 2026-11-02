# Windows forced-restart admission timeouts on main bfac6d6 (2026-11-02)

Part of `follow-up-windows-admission.md`. This record covers what was measured, what was inferred and what remains open.

## Original failure (preserved, not reconstructed)

- Run 41533898656, `quality (windows-latest)`, commit `bfac6d6` (tree identical to the passing pull request head `c1282ec`).
- `PersistenceRecoveryTests.PreservesExactStateAcrossForcedRestart` iterations 1, 3, 6 and 9 failed with
  `Crash worker did not reach the expected barrier: {"status":"failed","failureCode":"AdmissionTimedOut"}`. The other 428
  IntegrationTests passed; the canonical consumer was skipped.
- The worker reported only the category, so the stage, acceptance status, candidate and failing step are unknown. The
  candidates' stores were deleted with their private test directories. Neither can be recovered.

## Measured from that run's TRX

- In completion order, the iterations took 2.0, 1.9, 2.0, 2.5, 2.0, **30.9, 35.5, 28.0, 25.7**, 6.2 s. The four failures form
  one block from 22:11:33 to 22:13:33 UTC; afterwards the run recovered.
- No other test of the suite overlapped any of these iterations. A parallel-test overlap is therefore not supported.
- Across 264 tests present in both runs, the median per-test duration ratio against the passing run 41531699676 on the same
  tree is 1.03. The slowdown was concentrated, not general.

## Attribution added (PR #18)

The worker now times each setup step. A failure names the step, stage, acceptance status and request identity, and the
harness looks a reported candidate up read-only after stopping the worker. These are test and validation-host changes only.

## Operating envelope, PersistenceRecoveryTests (10 iterations per run)

| Run | Worker setup per iteration | Admission submits (median / max) |
| --- | --- | --- |
| Linux control, local | 255–309 ms | 3 / 40 ms |
| Windows push run 41541633557 | 724–977 ms; one iteration 4584 ms | 24 / 1025 ms |
| Windows pull request run 41541658878 | 686–712 ms | 22 / 81 ms |

In the slow Windows iteration, every step slowed together:
- admission `completed-a`: 1025 ms;
- session creation: 1090 ms;
- dispatches: 467–470 ms;
- `running-started`: 387 ms.

No single step stalled alone.

## Inference (not measurement)

The evidence fits transient, process- or host-wide slow episodes on the Windows runner, varying in severity. In the
original run, an episode was strong enough for one admission to exceed the 5 s budget. No source defect was isolated:
- the failures were not monotonic;
- no file accumulation was observed;
- no overlapping test;
- the same tree passed Windows quality in four other runs.

The cause of the episodes (CPU, storage, scanning or scheduling on the runner) was not measured and remains unknown.

## Decision

- No product or timeout change.
- The 5 s default admission budget and the test's assertions stay as they are.
- A future failure of this kind now reports its stage, candidate and step times.

Open: the episode cause, and whether the default budget needs a documented operating-envelope statement for Windows hosts
under load. Both stay with this follow-up.
