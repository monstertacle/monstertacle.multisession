# Defending review (status quo / accept as is): candidate 9e8b4870b46e

- Role: defending implementation reviewer. Read-only. Nothing was built, run or edited except this file.
- Start: 2026-11-01T12:25:08Z (`date -u`). End: 2026-11-01T12:29:30Z (`date -u`). About 20 tool calls.
- Candidate: `/home/user1/dev/.agenttemp/delivery-review-9e8b4870b46e` at 9e8b4870b46ec7d662ce955344819357798b449e, base 5d4aa2f.
- Reference: the frozen `SharpClaw.Listener` sources under `event-boundary-architecture-20261101/change/workspace/server/src/SharpClaw.Listener`.

## Verdict

**Accept without code changes.** I found no P0 or P1 defect and no P2 defect in production code. The weaknesses I could not
defend are a gap in the tests (P2, test-only), two inaccurate documentation sentences (P3) and three minor P3 items (one is
dead code). Each one can be fixed with a test or doc change of one or two lines. None needs a change to the design, and
none widens the scope.

## Defended points (checked against the source)

1. **Exact current format, so a live checkpoint stays usable.**
   - The four DDL strings in `CheckpointFormat.cs:12-15` are byte-identical to the Listener's: `Store.cs:26`,
     `PreparationRearms.cs:85`, `DeliveryReplays.cs:62` (shown as line 159 in my concatenated read) and
     `DeliveryRebinds.cs:73`.
   - The implicit-index allow-list (`CheckpointFormat.cs:27-33`) matches the constraints: no index for the INTEGER PK; one
     PK index each for rearms and rebinds; PK plus UNIQUE for replays.
   - The Listener keeps lifecycle and subagent stores in separate files. `LifecycleConfig.cs:16` refuses
     `StatePath == owner.StatePath`, and `SubagentReporterConfig.cs:16-17` does the same. So the package's
     refuse-unknown-objects rule (`CheckpointFormat.cs:74-77`) cannot reject a live delivery checkpoint.
   - Rebuilding from the captured DDL in `ListenerFixture.cs:39-44`, followed by the verified opens in
     `CurrentFormatTests`, tests this against real Listener output.
2. **Same lease, so the package and the running Listener exclude each other.** Both use `<state>.lock` with `FileShare.None`
   (`CheckpointFile.cs:8,29`, `PrivateState.cs:58`; `Store.cs:19`). Contention becomes `ExclusiveAccessUnavailable`
   (`PrivateState.cs:60-63`).
3. **CAS predicates match the reference or are stricter.**
   - Stage: `DeliveryCheckpoint.cs:64` matches `Store.cs:52`.
   - Sending and preparation failure: `DeliveryCheckpoint.cs:76,82` match `Store.cs:57,62`, plus an `int.MaxValue` guard.
     That guard protects the Listener's `GetInt32` at `Store.cs:48`.
   - Uncertain: `DeliveryCheckpoint.cs:88` matches `Store.cs:66`.
   - Accept: `DeliveryCheckpoint.cs:103` uses `cursor=cursor+1` where the Listener uses `cursor=$e.Cursor` (`Store.cs:70`).
     The two are equivalent, because a pending item only exists at `cursor = position-1`. Stage enforces this, and no
     grant changes the cursor.
   - Crash conversion: `CheckpointOpening.cs:24` matches `Store.cs:34`, and only the owner open does it.
   - Resend CAS: `ResendJournal.cs:42` matches `DeliveryReplays.cs:170`.
   - Route CAS: `RouteJournal.cs:41` is equivalent to `DeliveryRebinds.cs:264`. The reference key requires phase
     `pending` with a pending item (`DeliveryRebinds.cs:210`), so `phase IS $phase AND pending IS $bytes` reduces to
     the hard-coded predicates.
4. **Atomicity is stronger than the reference.**
   - `GrantJournal.cs:41-60` runs everything inside one `BEGIN IMMEDIATE` (`deferred: false`): the idempotent lookup, the
     read of the expectation (`:50`), uniqueness, capacity, insert, CAS and commit.
   - The reference validates outside its transaction (`PreparationRearms.cs:82-83`; `DeliveryReplays.cs:155-157`), and the
     rearm grant never re-checks the row inside the transaction.
   - Rollback happens through `using` disposal on any exception, including the cancellation check just before commit
     (`:58`).
   - No connection or callback leaves the package: `Connection` is `internal` (`DeliveryCheckpoint.cs:22`), and the
     protected members belong to internal classes.
   - `AtomicityTests` proves the rollback with a real trigger.
5. **Idempotency, unique consumption and capacity.**
   - The lookup by request id comes first (`GrantJournal.cs:42`), just as `Existing` comes first in `DeliveryReplay.cs:50`
     (shown as line 88 in my read) and `PreparationRecovery.cs:284`.
   - Each resend item is consumed at most once: the count at `ResendJournal.cs:23-27`, the `UNIQUE` column and the
     serialized writer all enforce it.
   - The capacity check is `>= MaximumRetainedGrants` per table (`GrantJournal.cs:139-145`), with default and maximum 1024
     (`DeliveryLimits.cs:13`). This equals the reference constants `PreparationRearms.cs:11`, `DeliveryReplays.cs:108` and
     `DeliveryRebinds.cs:192`. Nothing is evicted.
   - Requiring strictly increasing attempts (`AttemptJournal.cs:48-60`) is weaker than the Listener's allowance-of-3 rule
     (`PreparationRearms.cs:64,70`). That is correct: allowance is host policy, and the package never allows what the
     reference refuses.
6. **Payload and receipt bytes are kept exactly.**
   - Input is strict UTF-8 with no NUL bytes (`CheckpointInputs.cs:34-54`).
   - The database encoding is verified as UTF-8 (`CheckpointFormat.cs:56-61`).
   - Reads use `CAST(pending AS BLOB)` (`CheckpointRow.cs:8`), so the stored text comes back as its exact UTF-8 bytes.
   - The digest is lowercase hex SHA-256 (`PendingItem.cs:23`), identical to `Wire.Digest` (`SharpClaw.Protocol/Wire.cs:29`).
     `PendingDigestEqualsTheDigestTheListenerRecorded` pins this.
   - Receipts are stored and returned unchanged. `StoredListenerReceiptsAreReturnedByteForByte` checks this against the
     Listener's output.
7. **The future host adapter can be mapped without losing an invariant.**
   - Replay: Find, then the host's checks (`DeliveryReplay.cs:53-59`), then `AuthorizeAsync` (which commits Sending), then
     the host's effect, then `AcceptAsync` or `MarkUncertainAsync`.
   - Rebind, already moved (`DeliveryRebind.cs:14-19`): `OpenExistingAsync` with the new route, then `RouteMoves.FindAsync`.
     `ListenerRouteMoveRepeatedAfterTheMoveAnswersFromItsReceipt` covers this.
   - Rearm history: `AttemptGrants.ListAsync` returns exactly what `Exhausted()` needs: the scope query is the same and the
     order is by attempts (`AttemptJournal.cs:8,26` vs `PreparationRearms.cs:49`).
8. **Private access is not weaker than the reference.**
   - Unix: directory 0700, as in `Store.cs:18`. Database and lease are created 0600 via `UnixCreateMode`
     (`PrivateState.cs:44-50,66-73`), which is stricter than the umask-created database of `Store.cs:22`.
   - The current Listener is Linux-only: `File.SetUnixFileMode` throws on Windows, and the harness says so
     (`New-CurrentListenerFixture.ps1:12`). So the Windows path cannot weaken any existing live checkpoint.
   - On Windows, the directory gets a protected DACL for the owner only, inherited by its children
     (`PrivateState.cs:17-38`). The calls that exist only on Unix sit behind the early `return`.
9. **Cancellation and failure translation.**
   - Every entry point checks the token before it does anything (`DeliveryCheckpoint.cs:51,116,140`;
     `GrantJournal.cs:21,38`), and grants check it once more before commit.
   - Only BUSY/LOCKED and CORRUPT/NOTADB are translated (`StorageFailures.cs:8-26`); everything else propagates, as the
     XML doc says.
   - A refused open releases the connection and the lease (`CheckpointFile.cs:47-53`, `DeliveryCheckpoint.cs:123-127`),
     matching `Store.cs:37-41`.
10. **Neutrality and size.**
    - No reference to SharpClaw, Listener or AgentFramework in `src/Monstertacle.Multisession.Delivery`. I grepped and
      found nothing. The csproj references only `Microsoft.Data.Sqlite`.
    - Public names are host-neutral. The format names stay private, as the ADR allows.
    - All files are 149 lines or fewer, and `codemetr-full.json` reports `"violations": 0`.
11. **Fixture provenance.**
    - The harness checks the release's commit and each file hash before running (`New-CurrentListenerFixture.ps1:14-19`).
    - It works only on private copies under `.agenttemp` (`:23-25`).
    - `oracle.json` records `listenerCommit 2c4a8af…`, the sha256 of each database, and the Listener's exact outputs.
12. **CI.**
    - The matrix includes `windows-latest` (`ci.yml:16`).
    - `Delivery.Tests` is added to both the test loop and the coverage loop (`Invoke-Quality.ps1:54,87`).
    - The coverage include list and `coverage-scope.json` are extended.
    - The package-layout check now expects eight packages.
    - The consumer test runs against both the local and the canonical packages.

### Evidence spot-checks (my own)
- `green-implementation-tests.log`: "Total tests: 74 / Passed: 74".
- `red-skeleton-tests.log`: "Passed: 3 / Failed: 71". The source has 61 `[Fact]`/`[Theory]` attributes, and the theories
  plausibly expand to 74 tests.
- `consumer-report.json`: the observed enum ordinals match the source. `ExclusiveAccessUnavailable`=4,
  `TransitionConflict`=6, `StaleExpectation`=7, `AllowanceConsumed`=9 (`DeliveryRefusal.cs`); `Sending`=1,
  `Accepted`=3 (`DeliveryPhase.cs`).

## Weaknesses of my own position (findings I cannot defend)

| Id | Sev | Location | Failure scenario | Minimal fix |
| --- | --- | --- | --- | --- |
| D1 | P2 (tests only) | `GrantJournal.cs:111-113` vs `AttemptGrantTests.cs` | For resends and route moves, the CAS in `ApplyAsync` re-checks route, cursor and attempts. Attempt grants have no CAS, so `Matches` is their only state guard. No attempt-grant test uses a stale route, position or attempts (only phase, `AttemptGrantTests.cs:67`). A mutant that drops `state.Attempts == expected.Attempts`, `RouteId` or `Position` from `Matches` survives: every resend stale test (`ResendAuthorizationTests.cs:82-91`) is still caught by the CAS. Such a mutant would record a rearm grant against a counter the host never saw. | Add a stale-expectation theory for `AttemptGrants.RecordAsync` (route, position, attempts, digest) that asserts `StaleExpectation` and an unchanged journal. |
| D2 | P3 (tests only) | `GrantJournal.cs:58` | Removing the check just before commit survives, because the cancellation tests cancel before the call. | Optional: drop the check, since the API doc only promises cancellation "before commit", or test it with a token canceled by a trigger. Leaving it is harmless. |
| D3 | P3 (doc) | `docs/delivery.md:35` | "Different values for the same id are a `RequestConflict`" overstates the contract. Only the stored columns are compared (`ResendJournal.cs:15`: item only; `RouteJournal.cs:23-25`: no attempts or digest; receipt text is never compared). `spec.md:72` (FR-012) states it correctly. The reference `Existing()` compares the full request digest (`DeliveryReplays.cs:134-137`). A host adapter that trusts this sentence and skips its own receipt check would answer success to a reused id carrying another expectation. | Reword the line to match FR-012: only stored columns are compared, so the host verifies its receipt (request digest) on `Recorded == false`. |
| D4 | P3 (doc) | `docs/delivery.md:48` | "Both platforms are tested in native CI": no Windows CI result exists yet, so the Windows code paths are verified only by reading. | Keep the claim only once the Windows job is green, or say "configured". |
| D5 | P3 | `PrivateState.cs:30-33` | The owner open replaces the DACL of an existing directory. If a host points the state path at a shared directory (for example its profile root), SYSTEM and Administrators lose access. This is the Windows counterpart of the Unix `chmod 0700` on an existing directory, which matches `Store.cs:18`, so it is consistent with the reference. It is still a hazard specific to Windows. On Windows, an existing database or lock file that has its own protected permissive DACL also stays readable, because of bypass-traverse checking. | Document that the state directory must be dedicated to the checkpoint. No code change is needed. |
| D6 | P3 | `CheckpointFormat.cs:46` | `Text(DeliveryPhase)` is never called (grep in `src` and `tests`). It is dead code against YAGNI and adds an uncovered line. | Delete it. |
| D7 | P3 | `StorageFailures.cs:13-27`, `DeliveryCheckpoint.cs:146` | If the token is canceled during a statement, Microsoft.Data.Sqlite interrupts it and surfaces `SqliteException` (SQLITE_INTERRUPT), not `OperationCanceledException`. It is never success, and the transaction is rolled back, so no invariant is lost. Only the exception type differs from what callers might expect. (Not run; this is inferred from reading.) | Optional: map the interrupt code to `OperationCanceledException` when the token is canceled. |

There are no P0 or P1 items. The scope boundary was not exceeded, and none of the fixes above adds a feature.
