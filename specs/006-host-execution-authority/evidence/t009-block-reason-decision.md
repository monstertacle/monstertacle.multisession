# T009: frozen decision on a distinct block reason for an unavailable framework continuation

Status: **decided, alternative C (root decision of 2026-11-03T18:45:58Z)**. Source: `main` at `4b533c9`; decision against `a24e548`.

## Measured source facts

- `SessionBlockReason` (`Contracts/SessionBlockReason.cs`) has four members: `None`, `RecoveryRequired`, `ProviderUnavailable`,
  `StorageFailure`.
- In SDK code, `ProviderUnavailable` has exactly two policy setters (incomplete as a list of writers; see Corrections):
  - `SessionBindingPolicy.AfterCompletion` (`Sessions/SessionBindingPolicy.cs:13-14`): the reply reports
    `ContinuationAvailable is false`, or error `ProviderSessionExpired`. `ConfigurationUnavailable` stays false.
  - `SessionAvailabilityPolicy.ForConfiguration` (`Sessions/SessionAvailabilityPolicy.cs:23-28`): the registered provider is
    unavailable. `ConfigurationUnavailable` becomes true, and `AvailabilityError` is set.

  `ISessionStore.MarkSessionBlockedAsync` is public, but the SDK only passes it `RecoveryRequired` (`SessionCatalog.cs:87`).
- Only the configuration case is cleared again: `ForConfiguration(available: true)` clears `ProviderUnavailable` only while
  `ConfigurationUnavailable` is set. A lost continuation therefore blocks the session until it is closed (within the SDK's own
  paths only; see Corrections). It reads exactly like
  a configuration outage, apart from `ConfigurationUnavailable == false`.
- Persistence: SQLite stores `BlockReason` as an integer, in `Sessions.BlockReason` and inside `SnapshotJson`. No enum-name
  converter is used. Readers accept only defined members: `SessionStoreConsistency.StateMatches` requires
  `Enum.IsDefined(snapshot.BlockReason)` (`Storage.Sqlite/SessionStoreConsistency.cs:46-48`). An existing release (alpha.8)
  would therefore refuse a store that holds a new member.
- Host consumer (SharpClaw `main` at `511a24a`, read only): `RuntimeRequestReconciliation.cs:25` only tests
  `BlockReason != SessionBlockReason.None`. No exhaustive switch was found. A new member does not break the host build, but
  the host's message ("closed or blocked after an uncertain execution") would describe the case imprecisely.

## Alternatives

| | Change | Persisted format | Public API | Host impact |
| --- | --- | --- | --- | --- |
| A | New member `SessionBlockReason.ContinuationUnavailable`; `AfterCompletion` sets it | Value domain grows. alpha.8 readers refuse such stores. A rollback to alpha.8 after one occurrence fails at open. Needs a decision: keep `SqliteSchema.Version` 2 and record the one-way change, or raise it to 3. Raising it is a host state-protocol step (SC-302) with fresh roots. | Additive enum member | Host message and docs; with a format step, host state protocol 3 → 4 |
| B | No new member. A derived, non-persisted `SessionSnapshot.ContinuationUnavailable` (`BlockReason == ProviderUnavailable && !ConfigurationUnavailable`), documented and tested on both setters | None | Additive read-only property | Host may use it to explain the block; no state-protocol step |
| C | Documentation only, in `contracts/public.md`: the distinction is `ConfigurationUnavailable` | None | None | None |

The review that raised this (review-r2 change P2-4) allowed "make the cause distinguishable, or document it". The T009 text
asks for a distinct block reason, which is only literally met by A. A is the only alternative that changes what is persisted
and what older releases can read.

## Historical recommendation before the root decision

B. It makes the cause distinguishable without changing the persisted format or the readers of existing releases. A should be
taken only together with an explicit store-format and host state-protocol decision. Whichever is chosen, implement it red first
with both setters covered, and record the host impact.

At that open-decision checkpoint no enum, property or format change had been implemented. The decision and current contract are recorded below.

## Decision (root, 2026-11-03)

Alternative C: document the existing contract. This supersedes the recommendation B above.

Reason: the public `ISessionStore.MarkSessionBlockedAsync` can set `ProviderUnavailable` with `ConfigurationUnavailable == false`
without any continuation. A derived property would therefore present persisted state as proof of a historical cause.

Implemented:
- `contracts/public.md` documents both normal pathways, the public-setter countercase and the revalidation rule.
- `IntegrationTests/ProviderBlockProvenanceTests` pins them on SQLite, across a reopen. It is a regression pin of existing
  behaviour, not a behavioural Red. Its mutation control (configuration revalidation also clearing a block it did not set) fails;
  the executed mutant is recorded under Mutation record.

There is no enum, property, store-format or state-protocol change.

## Mutation record (executed 2026-11-03T18:54Z, Linux, before commit `4669501`)

- Source: the working tree that was then committed as `4669501`. Test file blob `38bdb0a`
  (`ProviderBlockProvenanceTests.cs`, unchanged since); policy file blob `777230b` (`SessionAvailabilityPolicy.cs`, restored after
  the run and unchanged since `4b533c9`).
- Mutant: one line in `SessionAvailabilityPolicy.ForConfiguration` (line 14),
  `if (available && session.ConfigurationUnavailable)` replaced by
  `if (available && (session.ConfigurationUnavailable || session.BlockReason == SessionBlockReason.ProviderUnavailable))`. With it,
  a successful configuration check also clears a block that the configuration check did not record.
- Command: `dotnet test tests/Monstertacle.Multisession.IntegrationTests -c Release -f net10.0 --filter
  "FullyQualifiedName~ProviderBlockProvenanceTests"`.
- Observed: exit code 1, 1 failed of 1. The assertion at `ProviderBlockProvenanceTests.cs` line 55 (the completion-path session
  after revalidation and reopen) expected `(ProviderUnavailable, False, null)` and found `(None, False, null)`.
- The same filter on the unmutated source passed 1 of 1. The private run logs are kept outside the repository with SHA-256
  `d33eacb390cea65a7f7fd90835db26768ab328df987fbacd26c63366b8f13278` (mutant) and
  `a4645730c75bee1ad68eb0c439207391089ca1f3f240ce68d0385a90356a29ce` (unmutated).

## Corrections (2026-11-03, after review)

The measured-facts section above is kept as written at decision time. Two of its statements are incomplete:
- Writers of `ProviderUnavailable`: besides the two policy setters, the dispatcher's registration-mismatch path
  (`RequestDispatcher`, a `Failed`/`NotStarted`/`InvalidConfiguration` reply with `ContinuationAvailable == false`) reaches
  `AfterCompletion` through the same completion commit and leaves the same shape. The public `MarkSessionBlockedAsync` is a
  further writer that store callers can use with any reason; on SQLite it replaces every reason except `RecoveryRequired`.
- "Blocks the session until it is closed" is not a lifetime contract. The start-time reopen guard in
  `RecoveryCoordinator.PrepareAsync` and `ForConfiguration` leave this block unchanged. Other successfully committed
  transitions can replace the reason: `UnknownTransactions.Apply` sets `RecoveryRequired`, and explicit recovery decisions
  can change it again. Public `MarkSessionBlockedAsync(sessionId, None)` can also remove a `ProviderUnavailable` block.

The configuration-path statement above also requires its guard: `ForConfiguration` changes the flag only when the block reason is `None` or `ConfigurationUnavailable` is already true. A flag-false provider block is left unchanged.

`contracts/public.md` states the corrected contract. Choice C is unaffected: the shared stored shape does not prove its historical cause.
