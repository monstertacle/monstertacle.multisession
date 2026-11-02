# Research: Checkpoint base contracts and the delivery checkpoint package

**Source state**:
- `main` at `a44cb4f9cb00476d1abd51a6a43a34bde10913d8`;
- this branch merges the pinning commits `1032b84` and `6df1707`;
- the concatenated tracked sources of `src/Monstertacle.Multisession.DeliveryCheckpoints` have SHA-256
  `c866e16686d0b0b528ca2a8ce433b748cb0f42fd05abbcac0f30f176645966b2`.

**Consumer state**: the SharpClaw host at `ea49662ae6080cc42f3e7015ed15dbfd9314a13c` (read only; not changed by this feature).

## R1. Current public surface (namespace `Monstertacle.Multisession.DeliveryCheckpoints`)

| Type | Kind | Responsibility |
| --- | --- | --- |
| `DeliveryCheckpoint` | sealed class, `IDisposable` | Static `OpenAsync`, `OpenExistingAsync`. Instance `ReadAsync`, `StageAsync`, `MarkSendingAsync`, `RecordPreparationFailureAsync`, `MarkUncertainAsync`, `AcceptAsync`, `Dispose`. Internal `Options` and `Connection`. |
| `DeliveryCheckpointOptions` | sealed class | Path, channel, initial position, payload bound, grant capacity, busy timeout |
| `DeliveryChannel` | record | Consumer, source and route identity |
| `CheckpointState` | record | Position, pending item, phase, attempts, acknowledgement, route; `ToExpectation()` |
| `CheckpointExpectation` | record | Exact key of a grant (route, position, digest, phase, attempts) |
| `PendingItem` | sealed class | Stored bytes, position, SHA-256 digest |
| `DeliveryPhase` | enum | Pending, Sending, Uncertain, Accepted |
| `DeliveryRefusal` / `DeliveryRefusalException` | enum / exception | 13 typed reasons covering open, transition and grant |
| `DeliveryLimits` | static class | Format and input bounds |
| `AttemptGrants`, `ResendAuthorizations`, `RouteMoves` | sealed classes | Grant helpers, each constructed from the concrete `DeliveryCheckpoint` |
| `AttemptGrant`, `ResendAuthorization`, `RouteMove`, `GrantOutcome`, `StoredAttemptGrant` | records | Grant requests and results |

The internal types are:
- `CheckpointFile`: lease plus connection;
- `CheckpointOpening`;
- `CheckpointFormat`: canonical DDL, phase strings, version 1;
- `CheckpointRow`;
- `CheckpointInputs`;
- `PrivateState`: 0700/0600 and the Windows ACL;
- `StorageFailures`;
- `GrantJournal<T>` and its three journals;
- `GrantKey`.

The grant journals use `DeliveryCheckpoint.Connection` and `Options` inside one library-owned transaction
(`GrantJournal.cs`, `RecordAsync`). Grants therefore cannot work through an interface without leaking the connection.

## R2. Actual consumers

| Consumer | Uses |
| --- | --- |
| Host `ListenerCheckpoint.cs` | Field `DeliveryCheckpoint checkpoint`. `OpenAsync` / `OpenExistingAsync` with `DeliveryCheckpointOptions(path, DeliveryChannel(...))`. `ReadAsync`, `StageAsync`, `MarkSendingAsync`, `RecordPreparationFailureAsync`, `MarkUncertainAsync`, `AcceptAsync`. Maps `DeliveryPhase` to its four status tokens and back to `CheckpointExpectation`. Exposes the concrete store as `Package` for grants. |
| Host `PreparationRearms.cs`, `DeliveryReplays.cs`, `DeliveryRebinds.cs` | `new AttemptGrants(store.Package)`, `new ResendAuthorizations(store.Package)`, `new RouteMoves(store.Package)` |
| Host `CheckpointRefusals.cs` | `switch` on `DeliveryRefusalException.Reason`: separate maps for open, transition and grant (family-specific) |
| SDK `eng/DeliveryConsumer.cs.txt` | Isolated invoice-outbox consumer: the whole public surface, nine conformance cases |
| SDK delivery tests | 20 files. They use internal `Options` and `CheckpointFormat` through `InternalsVisibleTo` (FormatGuardTests, OpenTests, CorruptionTests and others). |

No other SDK package references the delivery package. The session packages do not reference it, and it references none of
them (csproj: `Microsoft.Data.Sqlite` only).

## R3. Persisted format versus namespaces

- **Decision**: a namespace and assembly move needs no format change.
- **Rationale**: every persisted value comes from string constants in `CheckpointFormat`:
  - the four table definitions, compared exactly with `sqlite_master.sql`;
  - the implicit index names;
  - the phase text;
  - `Version = 1`;
  - the `.lock` suffix in `CheckpointFile`;
  - parameterised values.

  No assembly, namespace or CLR type name is stored. A grep for `GetType()`, `AssemblyQualified` and `FullName` found none;
  the only `typeof` values are a SQL function and a CLR field-type check. Exception messages are not persisted. The host maps
  `Reason`, not message text.
- **Consequence**: files written by alpha.7 open in the new package unchanged. The current-format fixtures prove this when
  run against the moved code. The format stays at version 1. No state-protocol change is implied for the host by the SDK
  format, although the host's own package-identity binding changes (see R7).

## R4. Contract candidates and the decision

**Decision (2026-11-02T20:51:58Z)**: candidate 1 and 2 as written below, no opener (3). Root rejected moving delivery transitions, phases or grant refusals into the base (r1 disposition). The v2 delta candidate was
outlined below; root's final direction is pending.

1. **Base**: a strongly typed, storage-independent read and lifetime contract over a caller-chosen snapshot,
   `ICheckpoint<TSnapshot> : IDisposable` with `Task<TSnapshot> ReadAsync(CancellationToken)`.
2. **Delivery**: `IDeliveryCheckpoint : ICheckpoint<CheckpointState>` adds the five transitions. `DeliveryCheckpoint`
   implements it. Grant helpers keep the concrete type.
3. **Opener**: only if a caller needs it. The only caller today composes the concrete store through static methods
   (`ListenerCheckpoint.Open` / `OpenExisting`). No caller is generic over storage. Evidence therefore currently favours no
   base opener; a delivery-local opener port is optional.

Alternatives and their source objections:

| Alternative | Objection |
| --- | --- |
| Base `ICheckpointCustody` with Stage/MarkSending/Uncertain/Accept (r1 proposals) | Delivery state machine in the base; rejected by root |
| Untyped `object` or phase strings in the base | Loses type safety; persisted strings would leak into the contract |
| One interface per class | Ceremony without callers |
| Base-only marker or forwarders | Forbidden by the owner commission |
| Grants on the port | Needs the private transaction; would expose the connection or duplicate it |

Points the final decision must fix:
- the exact generic name and variance;
- whether `ICheckpoint<T>` derives from `IDisposable` or `IAsyncDisposable`. Today `DeliveryCheckpoint` implements only
  `IDisposable`, and `CheckpointFile.Dispose` is synchronous;
- whether a base opener exists;
- whether the base declares any exception type. The current proposal is none: refusals stay typed in Delivery.

## R5. Test plan (independent of the final signature where possible)

- **Move**: all 20 delivery test files move to `tests/Monstertacle.Multisession.Checkpoints.Delivery.Tests` with namespace
  changes only. `InternalsVisibleTo` follows the new test assembly name. Assertions stay unchanged, including
  `CurrentListener` fixtures.
- **Base tests** (`tests/Monstertacle.Multisession.Checkpoints.Tests`):
  - a typed test double implements the base contract;
  - checks for read, cancellation (cancelled token throws `OperationCanceledException` and causes no change) and disposal;
  - an architecture test proves the base assembly's references are limited to the BCL (red-first: temporarily add a
    reference).
- **Shared delivery port cases**: one abstract case set over the delivery port, run against the real store and an in-memory
  delivery double. Named mutation: the real store stops implementing the port, or a transition bypasses it.
- **Isolated consumers**:
  - `eng/Test-CheckpointsConsumer.ps1` (new): restore only the base package, implement the contract, run; its lock must
    contain no `Monstertacle.Multisession.Checkpoints.Delivery`, `Microsoft.Data.Sqlite`, `SQLitePCLRaw.*`,
    `Monstertacle.Multisession.Common` or `Microsoft.Agents*`;
  - the existing delivery consumer moves to the new package name and namespaces, with nine unchanged cases.
- **Inventory guards**:
  - `Invoke-Quality.ps1` compares its suite list with the solution's `tests/` projects (red-first: a solution test project
    absent from the list must fail);
  - `Test-PackageLayout.ps1` names nine packages;
  - `tests/coverage-scope.json` lists both new assemblies; `Test-Coverage.ps1` already rejects an unregistered `src` project.

## R6. Versioning and publication

- **Proposal**: `0.2.0-alpha.8` for the whole set (nine packages). On 2026-11-02 the nuget.org flat container listed only
  `0.2.0-alpha.7` for the SDK packages. It returned 404 for `monstertacle.multisession.checkpoints` and
  `monstertacle.multisession.checkpoints.delivery`.
- **Policy**: `docs/publishing.md` documents the trusted publishing scope `Monstertacle.Multisession.*` and the
  record-then-publish procedure. Publication needs a merged release record for a successful `main` CI push run. This feature
  does not publish.
- **Old package**: `Monstertacle.Multisession.DeliveryCheckpoints` gets no alpha.8. It stays at alpha.7 as published and is
  not deprecated or reissued by this feature.

## R7. Host consequences (later, separately granted step)

The host changes:
- its package reference and namespace imports;
- the `ListenerCheckpoint` field type, which may become the delivery port, with `Package` keeping the concrete type for grants;
- lock files;
- `eng/monstertacle-packages.json` provenance;
- the SC-302 binding of the SDK store format to its state protocol, if that binding names package identities.

The host's checkpoint files are unchanged (R3). The host change, clean-cache consumer gates and reader attestation are not
part of this repository change.

## R8. Staged pinning tests

`tests/Monstertacle.Multisession.ContractTests/PartialEvidencePinningTests.cs` comes from `feature/015-continuity-pinning`
(`6df1707`). It is merged normally, with history kept, and is unrelated to checkpoints. The source branch and its clone are
untouched.
