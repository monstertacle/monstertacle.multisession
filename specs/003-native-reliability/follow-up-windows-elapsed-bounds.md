# Windows elapsed-time bounds failed on a documentation-only head (T019)

Owner: Alexander Bleissem (diagnosis and source uptake); root (source decision, reruns and merge)
Status: open; cause unknown. Recorded 2026-11-04. Priority P2: a required native check is red, and no product effect is shown.
Origin: finding SDK-WIN-CANCEL-LATENCY-20261104, pull request #28 (`cc319fe`, which changes only
`specs/006-host-execution-authority/tasks.md`; runtime and tests are byte-identical to `main` `1bdac2e`).

## Observations (both on `quality (windows-latest)`, pull request run 41754114699)

The two attempts failed different assertions in different suites. They are recorded separately, and no shared cause is assumed.

| Attempt | Job | Failed test | Bound | Other suites |
| --- | --- | --- | --- | --- |
| 1 | 126326035132 (01:26:54Z to 01:36:07Z) | `IntegrationTests.EmbeddedConsumerTests.RunsCompletePublicEmbeddingFlowWithoutLibraryChangesOrServerProcess`, line 49 (1 of 436) | cancelling a running request must reach its end within 2 seconds | UnitTests 447 and ContractTests 266 passed |
| 2 | 126331776929 (01:42:34Z to 01:48:29Z) | `ContractTests.RuntimeCleanupTests.BoundsCleanupAndRetainsLeaseUntilStoreCloses`, line 29 (1 of 266) | a stop with a 200 ms deadline, while store disposal is held, must return its failure within 1 second | UnitTests 447 passed; IntegrationTests did not run, because the gate stops at the first failed suite |

- **Same head, push run:** the push quality run 41754108932 at the same head passed both tests on Windows, and both pass on Linux.
- **No elapsed values:** neither assertion logs the measured elapsed time, so the size of the overrun is unknown.
- **Log hashes** (SHA-256 of the failed-job logs, kept privately): attempt 1 `5d1ea024...909b`, attempt 2 `7ee69744...973f`.

## Paths measured

- **Cancellation** (`EmbeddedConsumerTests.cs:46-49`):
  - the timer starts before `Requests.CancelRequestAsync`, which records the cancellation durably;
  - the dispatcher cancels the provider token, and `ConsumerOwnedProvider` ends its delayed request as cancelled;
  - the store commits the confirmed end;
  - the test helper polls the request every 10 ms (`EmbeddedConsumerTests.cs:69-76`).
- **Stop deadline** (`RuntimeCleanupTests.cs:22-29`):
  - the timer starts before `StopAsync(200 ms)`;
  - the stop stops acceptance and cancels the workers;
  - it waits for the worker completion, then starts the shared cleanup, whose store disposal the test holds;
  - the 200 ms deadline must end the wait with `StorageUnavailable`.

  This bound concerns the deadline, not the cleanup duration. It is the same test as the earlier stop/dispose finding, but a
  different assertion: line 29, the elapsed bound, not disposal.

## Next steps

1. **Decide without rerunning.** The next native run of this head, or a fresh run, is root's decision. Do not rerun to get green.
2. **Diagnostic plan, pure C#, outside the repository** (under `~/dev/e2e-harness-bundles`, as in `follow-up-windows-admission.md`):
   - run both scenarios with per-stage timestamps (cancellation record, provider token cancellation, provider end, completion
     commit, read; stop entry, worker completion, cleanup start, deadline firing, stop return);
   - run on native Windows and on a Linux control, with fixed packages;
   - record the overrun and the slow stage.
3. **No change to a bound or to runtime code** without a measured envelope or an isolated defect. A defect needs an observed
   regression Red first.

## Required native tests

Both tests, unchanged, in a native Windows run of the decided head, plus the complete quality gate. If a defect is isolated, its
deterministic regression goes on both platforms.

## Acceptance

The slow stage of each observation is measured, or the remaining uncertainty is stated with the measured envelope, and root
decides the gate. Both 2 s and 1 s bounds stay as they are unless an envelope decision changes them. This record does not claim a
cause.

Related: `follow-up-windows-admission.md` (Windows admission variability), `follow-up-capacity-probe.md` and
`follow-up-dispose-stop-race.md`. No shared cause with them is established.
