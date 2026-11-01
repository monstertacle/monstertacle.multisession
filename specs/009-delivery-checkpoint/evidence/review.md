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

## Follow-up round (both reviewers, diff 9e8b487 → cdbffec)

Defender (12:40:10Z–12:41:04Z, about 6 tool calls): changes necessary and minimal, no new production defect; noted harness
lock-file loss and a misleading variable name (P3) and the missing concrete follow-up for C1 (P2, process).
Challenger (12:40:08Z–12:41:27Z, 6 tool calls): C2, C3, C4 (code/test), C6, C7, C9, D6 closed; disputed C1 process, C5 doc
wording, skeleton evidence; new N1–N3 (P3). Dispositions, fixed in the next commits:

| Item | Disposition |
| --- | --- |
| C1 concrete follow-up | `tasks.md` F-009-1: owner product integration actor, bounded job (private read-only copy, `OpenExistingAsync`, `ReadAsync`, three `FindAsync`, no writes, no live data committed), acceptance (no refusal, pending digest equals host key digest, receipts byte-equal, host suites green) |
| C1/D4 Windows | closed only by the green Windows job of the final head (`handoff.md`) |
| C5 | `DeliveryRefusal.ExclusiveAccessUnavailable` doc now names lease-file open failures and the inner exception |
| Skeleton evidence | mutation Red recorded in `tdd.md` |
| N1 | phase storage-class case now a blob equal to `pending` text |
| N2 | harness lock file kept as template, restore with `--locked-mode` |
| N3 / misleading name | snapshot written and merged inside the private work directory; variable renamed |

## Integration follow-up (alpha.6 head)

After PR #11 merged (main `8d9202a`), main was normal-merged (`984f866`, README the only shared file) and the delegated
root advance to `0.2.0-alpha.6` applied (`c8e116b`). Both reviewers checked the integration delta on a clean clone
(defender 13:00:30Z–13:01:04Z, about 6 calls; challenger 13:00:28Z–13:01:33Z, 7 calls): legacy deletions preserved,
no package identity reissued, delivery scripts consistent, earlier follow-up items all closed.

| Item | Disposition |
| --- | --- |
| I1 (challenger, P1): five lock files still pinned `[0.2.0-alpha.5, )` for project references; locked restore must fail | Stale ranges confirmed at source (`git grep`). The predicted failure did not occur: CI quality for `c8e116b` (run 41309101517) passed `restore --locked-mode --force` on Linux and Windows. Regenerated anyway to match the previous bump (`b35dc7d`) by one unlocked restore: only the 68 version lines in the same five files as the previous bump changed; forced locked restore then passed. Final head must be green in native CI |
| I2 / defender P3: handoff still lists alpha.4 bytes | Addendum with alpha.6 hashes from the final green head (`handoff.md`, `package-handoff.json`) |
| Defender P2: integrated head needs its own CI | Required before merge; recorded in `handoff.md` |
