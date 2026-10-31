# Observed TDD evidence

Baseline:372 unit tests green on source84d0c08 before behavior changes.

| Requirement | Executable test group | Observed semantic Red | Green evidence |
| --- | --- | --- | --- |
| US1/US2 | FrameworkSessionProviderTests / FrameworkFailureTests |14 failures/1 pass: missing continuation, final output, wrong certainty and no durable acknowledgement |combined-green:22 pass with evidence tests |
| US3 | EvidenceWorkflowTests |6 failures/1 pass: unchanged dependency fingerprints, no invalid-graph rejection, no accepted/blocked/Unknown results |combined-green:22 pass |
| Cancelled round | EvidenceContractTests.CancelledRoundDoesNotRunValidators |Cancellation surfaced as no-output failure after preparation |Full399-unit run includes regression |
| Noncooperative agent | FrameworkCancellationTests.DeadlineReturnsUnknownEvenWhenAgentIgnoresCancellation |Caller wait exceeded500ms instead of returning finite Unknown |Full399-unit run includes regression |

Initial test compilation/configuration issues were corrected before recording semantic Red; these are not presented as TDD evidence. Additional actual-SQLite regression qualification:3 framework tests pass, including runtime recreation, unknown custody and cancellation/serialization race across reopen. These strengthen already specified behavior and are not claimed as separate pre-implementation Red runs. New full native/package qualification supersedes intermediate narrow Green logs.

Final contrary implementation review found two liveness defects and root identified tight polling. `review-regressions-red` records3 semantic failures: prior/fresh noncooperative validation deadlines and pre-cancelled continuation availability. `observation-red` records pending-to-completed acceptance failure before interval support. After correction,36 targeted unit tests pass and the actual-SQLite regression verifies a cancelled restore followed by a successful later turn. A compile-time analyzer warning in the intentionally noncooperative fixture was corrected with explicit CancellationToken.None; it is not counted as semantic Red.
