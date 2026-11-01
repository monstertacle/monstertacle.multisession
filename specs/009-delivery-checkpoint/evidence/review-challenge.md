# Challenging implementation review (role: change-required)

- Candidate: `9e8b4870b46ec7d662ce955344819357798b449e` (clean clone `/home/user1/dev/.agenttemp/delivery-review-9e8b4870b46e`), base `5d4aa2fc693a43e3302cf50bd0ff8ecbbed7d435`
- Reference host: `.../event-boundary-architecture-20261101/change/workspace/server/src/SharpClaw.Listener` (HEAD `2c4a8af`)
- Started: 2026-11-01T12:24:54Z (`date -u`); ended: 2026-11-01T12:34:13Z (`date -u`); about 30 tool calls
- Mode: read-only. Nothing was built or run. The only file written is this report.

## Spot checks of the shared evidence (done at the source, not taken on trust)

- `red-skeleton-tests.log`: 74 total, 3 passed, 71 failed. The passing tests are `TransitionTests.ANoPendingStateHasNoExpectation`, `PrivateAccessTests.TheLeaseIsAnOperatingSystemExclusiveLock` and `OpenTests.ExistingCheckpointIgnoresANewInitialPosition`. `green-implementation-tests.log`: 74 of 74 passed (Linux). I counted 74 `[Fact]`/`[InlineData]` attributes in the test sources, which matches.
- `codemetr-full.json`: `passed: true`, 0 violations, and the Delivery types appear in it (`Delivery.AttemptJournal`, ...).
- `oracle.json` harness hashes: all 7 `fixture-harness/*` sha256 values match the committed files byte for byte. The listener commit is recorded as `2c4a8af…`.
- Stored DDL history in the reference repo: all four `CREATE TABLE IF NOT EXISTS` texts are the same in every commit that touches Store/PreparationRearms/DeliveryReplays/DeliveryRebinds (56b3234 … 2a306d4). There is no `ALTER TABLE` anywhere in the Listener. Phase strings across that history are only `pending|sending|uncertain|accepted`. So exact DDL verification (`CheckpointFormat.cs:70`) cannot reject a checkpoint written by any Listener release in that history. That part of the candidate is right.

## Findings

### C1 — P1 (acceptance gate, evidence not code): two proofs the authorized architecture requires are missing

- Where: `specs/009-delivery-checkpoint/spec.md:88-91` (SC-002, SC-003); `docs/delivery.md:47-48`; the architecture decision bullet C1/F1/B1/B2 ("Prove against fixtures from the CURRENT Listener **and a read-only live-state copy**") and bullet F3 ("actual Linux+Windows CI").
- Claim: SC-002 dropped the read-only live-state copy. Only harness-generated fixtures exist. Windows has no result at all. Even so, `docs/delivery.md:48` already says in the present tense: "Both platforms are tested in native CI."
- Failure scenario: a real live checkpoint may carry something the harness never produced, for example an older `delivery_rebinds` row with `pending_id NULL` from the 405dbfd "fresh move" era, a non-default page or encoding setting, or a WAL in some other state. Such a file could be refused as `CheckpointCorrupt` the first time the host adapter is activated. Separately, every Windows code path (`PrivateState.cs:17-38`, the Windows lease semantics) ships with no execution at all.
- Minimal fix: copy the live checkpoint read-only to a private directory outside the repo (no live data committed). On the copy, run `OpenExistingAsync`, `ReadAsync` and all three `FindAsync` calls, and record the hashes and outcome as evidence. Keep the doc sentence until Windows CI is green, or change it to the future tense.
- Confidence: high that this is required and missing. Medium on calling it P1 rather than a host-activation item; see the weaknesses section.

### C2 — P2: unique consumption depends on a host-attested `ItemId`, and the shipped example consumer shows the unsafe pattern

- Where: `GrantRecords.cs:14`, `ResendAuthorizations.cs:4-6`, `ResendJournal.cs:23-27`, `eng/DeliveryConsumer.cs.txt:81` (`new ResendAuthorization(Guid.NewGuid(), expected, Guid.NewGuid(), …)`) and `:104` (`var item = Guid.NewGuid();`). Doc guarantee at `docs/delivery.md:38` ("at most one authorized resend per item").
- Claim: the library enforces uniqueness on whatever GUID the host passes. Nothing ties that GUID to the stored pending bytes, position or digest. The reference derives it from the validated pending notification (`DeliveryReplays.cs:115`, `pending.EventId`). The "independent host" conformance consumer instead mints a random item id at authorization time. Its `second-resend-refused` case only passes because it reuses the same `request` via `with`.
- Failure scenario: a second host copies the example. Its operator tool builds each resend request from scratch with a new `ItemId`. Every `AuthorizeAsync` then succeeds, Uncertain goes to Sending again each time, and a non-idempotent destination receives repeated resends. The documented "at most one authorized resend per item" does not hold, and no refusal is ever raised.
- Minimal fix (no scope expansion): state in the `ItemId` XML docs and in `docs/delivery.md` that the id must be a stable, deterministic identity of the staged item, derived from the host's canonical payload, and that the guarantee is per attested id. Change the consumer to derive the id deterministically from its own invoice payload (for example a UUID from the invoice number) and reuse it for the attempt grant, resend and move. Add one consumer case showing that a fresh `RequestId` with the same derived id is refused. `research.md:57` already records the attested-id decision. The public docs and the example do not carry it.
- Confidence: high on the facts; medium on the severity.

### C3 — P2: idempotency doc overclaims what a replay is compared against

- Where: `docs/delivery.md:34-35` ("different values for the same id are a `RequestConflict`") against `ResendJournal.cs:15` (`StoredMatches` compares only `pending_id`) and the receipt never being compared in any journal (`GrantJournal.cs:128-130`).
- Claim: a repeated resend request id with a different `Expected` (other attempts, route, position or digest) and the same item returns `Recorded=false` and the old receipt instead of `RequestConflict`. A different receipt text is never a conflict either (the tests even pin this: `ResendAuthorizationTests.cs:246-248`, `RouteMoveTests.cs:390`). The reference refuses this case (`DeliveryReplays.cs:134-137`, `receipt.Before != request.Expected` → `delivery_request_conflict`).
- Failure scenario: a host trusts the doc and skips its own receipt check. An operator re-submits an old request id against a new expectation and gets "already authorized" with a stale receipt. The host then reports a resend authorization that does not match the current checkpoint. The adapter can still detect this only by decoding the receipt, as the reference does.
- Minimal fix: align `docs/delivery.md` with FR-012 (`spec.md:72-73`: "when its stored columns match"). Name the compared columns per journal, and state that the host must verify its own receipt content (Before or expectation) if it needs full equality. With the current `delivery_replays` columns the library cannot do more, so no code change is needed.
- Confidence: high.

### C4 — P2: on Windows the owner open silently replaces the ACL of whatever directory contains the state file, and the main Windows branch is unasserted

- Where: `PrivateState.cs:19-37` (`SetOwner` + `SetAccessRuleProtection(true,false)` + `SetAccessControl` on an existing directory, run *before* the lease at `CheckpointFile.cs:27-29`); test `PrivateAccessTests.cs:12-35` pre-creates the directory, so only the `SetAccessControl` branch is asserted.
- Claim: (a) The caller's path decides which directory is rewritten. `C:\Users\me\outbox.db` would make `C:\Users\me` owner-only, removing SYSTEM, Administrators and every inherited ACE, with propagation to its children. The Unix branch has the same shape (chmod 0700 of the parent) and matches the reference `Store.cs:18`. But the reference is Linux-only, so the Windows effect has no reference precedent and no doc warning. (b) A second, refused owner rewrites the live owner's directory ACL before it learns the lease is held. (c) The `info.Create(security)` branch, which is what every fresh checkpoint takes, is never asserted on Windows. The mutant `info.Create()` (default inherited ACL, so private access is weakened) survives the whole suite. This is the scenario F3 says must not be silently weakened.
- Failure scenario: on Windows, a fresh checkpoint directory is created with inherited, readable-by-others ACEs (under the mutant, or under a regression in `FileSystemAclExtensions.Create` semantics), and no test turns red. Or a host points at a non-dedicated directory, and backup or antivirus services lose access to the user's profile folder.
- Minimal fix: in docs and XML docs, require a dedicated state directory and say that its ACL or mode is replaced. Add a Windows assertion for the newly created directory (state directory absent before open) and for the database and lease files inheriting only the owner ACE. Optionally take the lease before rewriting permissions on an existing directory.
- Confidence: medium. The behavior is read from source; Windows is unverified.

### C5 — P3: `OpenLease` maps every `IOException` to `ExclusiveAccessUnavailable`

- Where: `PrivateState.cs:60-63`.
- Scenario: an inspection open on a read-only or full filesystem, or a path-too-long error while creating `<state>.lock`, is reported as "another owner holds the lease". A host or operator that waits or retries on that reason will wait forever.
- Fix: map only sharing or lock violations (Windows `0x80070020`/`0x80070021`, Unix `EWOULDBLOCK` HResult) and let other IO failures propagate unchanged, as `StorageFailures.cs:5` already does for SQLite.
- Confidence: medium.

### C6 — P3: position overflow can make the checkpoint permanently unreadable

- Where: `CheckpointInputs.cs:22` (accepts `InitialPosition = long.MaxValue`), `DeliveryCheckpoint.cs:61-68` (`position - 1`, unchecked), `:103` (`cursor=cursor+1`).
- Scenario: `InitialPosition = long.MaxValue`, then `StageAsync(long.MinValue, …)`. The prior value wraps to `long.MaxValue`, so staging succeeds, and `PendingItem.Position` wraps negative. Accept then runs `cursor+1`; SQLite overflows to REAL. From then on every `ReadAsync` refuses `CheckpointCorrupt` (`CheckpointRow.cs:16`), so the package has written something it rejects itself.
- Fix: refuse `position < 1` in `StageAsync` (InvalidInput), and refuse `InitialPosition == long.MaxValue` or add `cursor < 9223372036854775807` to the Accept predicate.
- Confidence: high on the mechanics; practical likelihood is low.

### C7 — P3: "refusals change nothing" does not hold for owner open

- Where: `docs/delivery.md:36`, `DeliveryRefusal.cs:3` against `CheckpointOpening.cs:13-20` and `CheckpointFile.cs:27-35`.
- Scenario: an owner open refused with `IdentityMismatch` or `UnsupportedFormat` has already changed the directory mode or ACL, created `<state>.lock`, and switched the file to `journal_mode=WAL`. That is a persistent header change on a future-format (`schema=2`) file that the package then refuses. The behavior matches the reference; the doc claim does not.
- Fix: scope the sentence to transitions and grants, or do the identity and version check before the WAL/CREATE statement when the table already exists.
- Confidence: high.

### C8 — P3: test gaps, with concrete surviving mutants

1. `AttemptJournal.cs:43-45`: delete any of the `configuration`/`route`/`cursor`/`pending_id`/`pending_digest` terms in `StoredMatches`. Only `Attempts` is exercised (`CurrentGrantTests.cs:353`).
2. `RouteJournal.cs:23-25`: drop the `old_route` or `pending_id` comparison. Only `NewRouteId` and `Position` are tested (`RouteMoveTests.cs:391-392`).
3. `CheckpointFormat.cs:56-61`: remove the `PRAGMA encoding` check. No UTF-16 database test exists, yet this check is what makes `CAST(pending AS BLOB)` (`CheckpointRow.cs:8`) return UTF-8 bytes.
4. `CheckpointOpening.cs:13`: remove `PRAGMA synchronous=FULL`. FR-003 and FR-004 list it, and nothing observes it.
5. `CheckpointRow.cs:16-17`: remove `typeof(cursor)` or the phase/receipt `IsTextOrNull` checks. Only pending (blob) and attempts are corrupted in `CorruptionTests.cs:48-55`.
6. `CheckpointOpening.cs:10`: change `checkpointRequired: !owner` to `false`. An inspection open of a file holding only journal tables then leaks a raw `SqliteException` ("no such table") instead of `CheckpointCorrupt`, and no test covers that case.
7. `GrantJournal.cs:58`: delete the pre-commit `ThrowIfCancellationRequested`. The cancellation tests use pre-cancelled tokens, so they stop at line 38.

Plus: `CheckpointFormat.Text` (`CheckpointFormat.cs:46`) is dead code (no caller in src or tests). Three tests were green against the "signatures-only" skeleton. One is the lease test, which weakens SC-001 ("every FR observed Red") for FR-004's lease part and suggests the skeleton was not signatures-only.

Fix: add the six small assertions, delete `Text`, and correct the wording of the red-evidence note.

### C9 — P3: fixture fidelity is logical, not storage-faithful

- Where: `fixture-harness/CheckpointSnapshot.cs:50-51` (anything that is not int64 is dumped via `GetString`), `ListenerFixture.cs:31-55` (rebuilt into a fresh file with a fresh header), `New-CurrentListenerFixture.ps1:162-170` (leaves `custody.db`/`uncertain.db` inside the test `Fixtures` directory; only `.gitignore` keeps them out, while `.gitattributes` now declares `*.db binary`).
- Consequence: the storage-class strictness (`typeof()` checks) and the encoding check are never exercised against bytes the Listener actually wrote. That is one more reason for the live-copy check in C1. Fix: record `typeof()` per column in the dump and assert it in `ListenerFixture`, and have the harness write its databases to the private work directory, not the repo.
- Confidence: high.

## Checked and not a finding (candidate matches the reference or is stricter)

- Atomicity: each grant is one `BEGIN IMMEDIATE` transaction that covers the stored lookup, the row read, the CAS and the insert (`GrantJournal.cs:41-59`). This is stricter than the reference, which validates outside a deferred transaction (`DeliveryReplays.cs:155-157`). No connection or callback leaves it.
- CAS predicates: resend (`ResendJournal.cs:42`) is equal to `DeliveryReplays.cs:170`. Move (`RouteJournal.cs:41`) is equivalent to `DeliveryRebinds.cs:264` for the only movable phase. Accept `cursor=cursor+1` is equivalent to `cursor=$c` given the Stage invariant.
- Refusal ordering: idempotent lookup comes first, then eligibility, stale, consumed, capacity. This matches the reference except that NotEligible and Stale are swapped. Only the code differs, and it is host-mapped.
- Byte fidelity: strict UTF-8 is required, NUL is refused, text is bound as TEXT, the read uses `CAST AS BLOB`, and the digest is taken over the stored bytes. `CurrentFormatTests.cs:126-135` checks the digest against the Listener's own `pendingDigest`.
- Lease: the same `<state>.lock` with `FileShare.None` (flock on Unix), so the package and a running Listener mutually exclude.
- Bounds against current Listener writes: identifiers up to 256 characters (Listener ids are at most 80, routes are 64-hex), acknowledgement up to 1024 (equal to the CommandAdapter bound), payload default 256 KiB (Listener frames are capped at 262 144).

## Weaknesses of my own position

- C1 may belong to the host-activation decision rather than package acceptance. The spec status says "no host activation", and the architecture text treats activation as a separate evidence decision. If the owner places the live-copy proof there, C1 drops to P2 (doc tense plus pending Windows CI). Windows CI is plainly not done yet, but the candidate marks T009 open, so that is honest in `tasks.md`.
- C2: the attested-id model was a deliberate, recorded decision (`research.md:57`), and the format has no other item column for `delivery_replays`. The library cannot do better without a format change, which is out of scope. The finding is mainly about docs and the example, not the code.
- C3: FR-012 itself is worded correctly. Only `docs/delivery.md` overclaims. Ignoring receipt text on a replay is intended and tested.
- C4: the Unix behavior is exact parity with the reference. My Windows concern rests on reading the code and on default Win32 propagation semantics, not on a run. A protected owner-only DACL is tightening, not weakening, so FR-005 is not literally violated.
- C5–C9 are low-impact. C6 needs a contrived host input. C7 matches reference behavior.
- I first suspected that an inspection open should not allow transitions. That is wrong: the reference resend path itself runs under `new Store(config, existingOnly: true)` (`DeliveryReplayCommands.cs:21`) and calls `Accepted`/`Uncertain`. The candidate is right, so I dropped it.
- The candidate is probably right on the strict DDL and phase verification, the transaction design, the CAS predicates, capacity semantics and the fixture provenance (hashes verified).
