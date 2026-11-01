# Contrary implementation review (feature 009)

Frozen candidate: `9e8b4870b46ec7d662ce955344819357798b449e`. Two reviewers in separate contexts with opposed mandates
(challenge / defend), same frozen code and the same evidence set. Reports: `review-challenge.md`
(sha256 `6cb74fe9…a4a90c5`, 12:24:54Z–12:34:13Z, about 30 tool calls) and `review-defend.md`
(sha256 `b459d5d3…6e6e6`, 12:25:08Z–12:29:30Z, about 20 tool calls). Neither found a P0 or a transaction/CAS defect.
Root verified every disposition below at the source; fixes are in `cdbffec` (follow-up diff reviewed narrowly, see end).

| Finding | Source check | Disposition |
| --- | --- | --- |
| D1 attempt grants rely only on the expectation check | `GrantJournal.cs` `Matches`; attempt journal has no CAS | Fixed: `StaleAttemptGrantTests` (route/position/digest/attempts); mutant "drop attempts" observed red (`mutation-expectation-attempts.txt`) |
| C8 surviving mutants: stored-column comparisons, encoding, storage classes, missing table | confirmed by reading | Fixed: conflict theories for attempt/route columns, `FormatGuardTests` (UTF-16, no checkpoint table, storage classes). `synchronous=FULL` is per connection and not observable from a test; kept as format requirement, accepted |
| D2/C8 pre-commit cancellation check untested | `GrantJournal.cs:58` | Removed; statements already observe the token, commit is never canceled |
| D6 dead `CheckpointFormat.Text` | no callers | Removed |
| C6 position wrap-around | `StageAsync` used `position - 1` | Fixed: position ≥ 1, `InitialPosition < long.MaxValue`; `PositionsCannotWrapAround` |
| C4/D5 Windows ACL path for a new directory untested; existing directory ACL replaced | `PrivateState.cs` | Test `AMissingStateDirectoryIsCreatedOwnerOnly` added; replacing permissions of the state directory is the documented contract (mirrors the current Unix chmod); docs now require a dedicated directory |
| C2 resend uniqueness depends on a host-supplied item id | `GrantRecords.cs`, research Q item identity | Accepted design (format has no other item column, decision C2/F2); XML/docs now require a deterministic item id; consumer derives it from the payload |
| C3/D3 docs overstated conflict detection | `docs/delivery.md` vs FR-012 | Fixed wording: stored columns compared, receipt text not; host verifies receipts as today |
| C7 docs "refusals change nothing" too broad | `CheckpointOpening.cs` | Fixed wording for owner open side effects |
| D4/C1 Windows evidence | CI run 41306298023 (Windows) | Windows quality job ran Delivery tests 74/74 natively, coverage 97.7987 percent; the job failed only on Cover counting fixture-harness files (fixed in `d5088cd`). Docs claim kept once the final head is green on Windows |
| C1 proof on a read-only copy of the live checkpoint | architecture decision | Not in this actor's authorization (no live state access). Owned follow-up for the integration owner, see handoff |
| C5 every `IOException` maps to `ExclusiveAccessUnavailable` | `PrivateState.OpenLease` | Accepted P3: inner exception preserved; lock contention is the only expected IO failure at that point |
| D7 mid-statement cancellation may surface as `SqliteException` | `StorageFailures.cs` | Accepted P3: transaction rolls back, never reported as success |
| C9 logical dump instead of files; `.db` left in Fixtures | harness | Repository forbids SQLite fixtures; dump keeps DDL text and storage classes (integers vs text); harness now deletes the files after hashing |
| Skeleton passed three tests | `red-skeleton-tests.txt` | Recorded in `tdd.md`; two tests tightened before Green |
