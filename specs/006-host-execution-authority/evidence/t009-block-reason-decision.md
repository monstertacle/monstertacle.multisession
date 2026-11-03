# T009: frozen decision on a distinct block reason for an unavailable framework continuation

Status: **open, needs a decision before implementation**. Source: `main` at `4b533c9`.

## Measured source facts

- `SessionBlockReason` (`Contracts/SessionBlockReason.cs`) has four members: `None`, `RecoveryRequired`, `ProviderUnavailable`,
  `StorageFailure`.
- In SDK code, `ProviderUnavailable` has exactly two setters:
  - `SessionBindingPolicy.AfterCompletion` (`Sessions/SessionBindingPolicy.cs:13-14`): the reply reports
    `ContinuationAvailable is false`, or error `ProviderSessionExpired`. `ConfigurationUnavailable` stays false.
  - `SessionAvailabilityPolicy.ForConfiguration` (`Sessions/SessionAvailabilityPolicy.cs:23-28`): the registered provider is
    unavailable. `ConfigurationUnavailable` becomes true, and `AvailabilityError` is set.

  `ISessionStore.MarkSessionBlockedAsync` is public, but the SDK only passes it `RecoveryRequired` (`SessionCatalog.cs:87`).
- Only the configuration case is cleared again: `ForConfiguration(available: true)` clears `ProviderUnavailable` only while
  `ConfigurationUnavailable` is set. A lost continuation therefore blocks the session until it is closed. It reads exactly like
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

## Recommendation (for root)

B. It makes the cause distinguishable without changing the persisted format or the readers of existing releases. A should be
taken only together with an explicit store-format and host state-protocol decision. Whichever is chosen, implement it red first
with both setters covered, and record the host impact.

Not implemented in this change: no enum, property or format change was made while this decision is open.
