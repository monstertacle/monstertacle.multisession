# SDK006 patch r2: change-advocate review

Reviewed against the frozen patch `patch-r2.diff`. The sha256 `d3b51d85…f3` matched. Base `0597d9e`. Read-only: no build, tests or Python. The line numbers `l.` refer to the patch file.

## P1: must change before release

**P1-1. A typed outcome can mark an invoked call NotStarted despite acknowledged evidence that it ran.**
- `FrameworkExecution.CompleteAsync` (l.79-92) checks only `Trustworthy`.
- `FrameworkOutcomeRules.IsKnownFailure` (l.258-262) accepts `NotStarted` whenever the output is empty.
- `DispatchReplyPolicy.cs:13,32` and `DispatchTermination.cs:44` pass it through.

An agent can therefore acknowledge `ObserveOperationAcceptedAsync` or `ObserveNativeSessionAsync` and then return `{Failed, NotStarted}`. Public `Evidence` then shows an `OperationReference` next to `NotStarted`, and the host may resubmit, so the external work runs twice.

Fix: once `OperationAccepted` or `NativeSessionObserved` has been acknowledged, typed `NotStarted` becomes Unknown. No current test covers this: `FrameworkOutcomeTests.cs:54,109,153` test NotStarted only without observations.

**P1-2. The prior-reader step breaks CI on both platforms.**
- `PackageSmoke.ps1` (l.78 of its hunk) always calls `Build-PriorPackages.ps1:9`, which runs `git archive 6127f2e`.
- In `ci.yml` only the first job sets `fetch-depth: 0` (l.26). The canonical-consumer job (l.86-103) uses depth 1, so the commit is missing and the script throws.

It passed locally only because the local repo is a full clone. Fix: give that job full history, or pass the prior feed along as an artifact.

## P2: should change

**P2-1. The prior-reader probe does not prove refusal before provider construction.**
- `PriorReaderConsumer.cs.txt` never registers a provider.
- The text "before any provider construction" is a hard-coded literal.

The probe proves only that the store open fails. The spec asks for refusal before provider construction or invocation. Prove it by going through the prior runtime with a counting factory and asserting a count of 0.

**P2-2. "Actual prior package" is rebuilt from source.**
- `Build-PriorPackages.ps1:14-17` packs with the SDK selected by the working directory's `global.json`.
- There is no hash comparison against the alpha.2 `contentHash` SharpClaw consumed, yet `Test-PriorReaderRefusal.ps1:4` claims "actually published".

Either compare the hashes or reword the claim.

**P2-3. Agent-supplied references are last-write-wins and unbounded.**
- `ObservationTransactions.cs:18` overwrites `OperationReference`. Up to 16 IDs collapse into one, in both `Evidence` and the recovery case (`UnknownTransactions.cs:37`).
- The sink does not fix the first value (`DispatchObservationSink.cs:57`), unlike `NativeSession` (l.54-56).
- `ProviderObservation` has no length or content bound. These strings now come from arbitrary agent code through a public API and appear on every `ListRequests` page.

Fix: reject conflicting `OperationAccepted` values, and add a byte bound.

**P2-4. A lost continuation reuses `SessionBlockReason.ProviderUnavailable`.**
- `SessionBindingPolicy.cs:13` sets it.
- `SessionAvailabilityPolicy.cs:20` clears it only in the configuration case.
- Framework has `RequiresOperationAwareRecovery = true` but no `IExecutionRecoveryProvider` (`FrameworkSessionProvider.cs:26`, `RecoveryService.cs:86-89`).

As a result, one serializer failure blocks the session permanently. The host cannot tell this apart from a transient configuration outage. Make the cause distinguishable, or document it in `contracts/public.md`.

## P3: may follow

- **No `Evidence` contract for store implementers:**
  - `ISessionStore` is public but has no documented duty to project `Evidence`.
  - `ControlledQueryStore` (the in-memory test store) leaves it null.
  - The SQLite Get, List and Cancel paths are consistent (`SqliteQueryStore.cs:27-42`, `SqlitePageQueries.cs:31-35`); facades, CQRS and ValidationHost inherit them.
  - `RecoveryCaseSnapshot` has no native reference.
- **Typed Unknown with partial output loses its error category:** the whole outcome is rejected (l.262) and the reply becomes `ExecutionFailed`. Dropping the output itself matches `DispatchReplyPolicy.Uncertain` and is acceptable. Normalize instead of rejecting so the category survives.
- **The cancellation race erases preserved output:** `DispatchTermination.cs:25-31` contradicts the blanket wording in the ADR addendum.
- **Late callbacks after `Close` are silent:** they set `_failed` without any diagnostic.
- **Unverified:** that `ChatClientAgent` keeps `FrameworkInvocation` out of `ChatOptions` and the wire. This needs a test with a recording `IChatClient`.
- **Windows:** only the scripts differ by platform. The hash snapshot is non-recursive (`Test-PriorReaderRefusal.ps1:44`). Nothing was verified on Windows outside CI.

## Checked and found correct

- No path after invocation maps to NotStarted: `invocation` is set before `RunAsync`, and both catch blocks yield Unknown (l.60-76).
- The pending-before-closed ordering stops success from overtaking an in-flight callback.
- On reopen, `RequestStoreConsistency` refuses persisted `Evidence`.
- MAF 1.27.0 is used through public APIs only.
- No retry or resume authority is introduced.

## Weaknesses of my own position

- **P1-1:** the spec explicitly trusts qualified agents to report not-started, and observations are evidence, not authority. The SDK overriding the agent edges toward second-guessing the host's qualification.
- **P1-2** is CI wiring, not architecture.
- **P2-1** adds a runtime harness to a format probe, even though opening the store may already be the earliest gate.
- **P2-4** adds public enum surface, which breaks exhaustive switches in the host.
- **P2-3:** a first-value rule may wrongly reject agents that accept several operations. A list would be more accurate but means more schema.
- All findings come from reading code. Nothing was executed.

Duration: `date -u` 18:37:20Z to 18:41:32Z at the first draft, about 5 minutes including this trim. About 30 tool calls.
