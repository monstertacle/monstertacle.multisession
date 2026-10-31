# Status-quo review: patch-r2 (feature 006, typed outcomes and observations)

Role: defend the existing boundaries. Patch verified: sha256 `d3b51d85…f3` matches. Base 0597d9e. I only read the code and ran no build or test. Line references point to the working tree.

## Verdict

None of the changes needs to be reverted. Every semantic change moves toward blocking or toward Unknown, never toward resuming. The Common layer still holds the authority. I found no P1. Below are three P2 items to narrow or document, and four P3 items.

## Checked concerns

**DispatchTermination (`Common/Requests/DispatchTermination.cs:47-48`).** The patch adds `reply.ContinuationAvailable is not false`. Two providers set `RequiresOperationAwareRecovery`: Framework (`FrameworkSessionProvider.cs:27`) and Interactions (`InteractionSessionProvider.cs:27`). Conversations does not (`ConversationSessionProvider.cs:24`), so the change cannot reach it. Interactions emits `ContinuationAvailable=false` with `Binding=null` only when the error is ProviderSessionExpired (`InteractionResponse.cs:53`). It produces that error only with NotStarted (`InteractionSessionProvider.cs:71`, `InteractionBindingPolicy.cs:18`), and NotStarted never enters the reopen branch. Its ConfirmedEnded failures carry either `known` or null with `ContinuationAvailable=null` (`InteractionResponse.cs:34-35,53`), so they still reopen. **Today Interactions behaves exactly as before.** Framework does not implement `IExecutionRecoveryProvider`, so before the patch `ReopenAsync` returned null (`:61`) and every confirmed Framework end in the race came out as Unknown. The patch lets these ends commit. `SessionBindingPolicy.cs:13` then blocks the session. Blocking is the conservative direction.

**Serialization failure (`FrameworkExecution.cs` TryEncodeAsync/CompleteAsync).** Before the patch the result was Failed, ConfirmedEnded, the *pre-turn* binding, `ContinuationAvailable=false` (`FrameworkResponsePolicy.Failure`). After the patch it is Succeeded with output, `Binding=null`, `ContinuationAvailable=false`. In both cases completion keeps `session.Binding` and blocks the session (`CompletionTransactions.cs:31-34`). The only difference is that state and output survive. The unchanged `data-model.md:7` ("Confirmed end/output survive serializer failure") and `spec.md:12` require exactly this, and they do not limit it to the typed path. **The changed test is justified.** The old assertion `Assert.Equal(RequestState.Failed…)` pinned the defect.

**Single source of truth for evidence.** Evidence lives only in the `StoredDispatch` envelope (`StoredDispatch.cs`). It is projected at read time (`QueryTransactions.cs` PublicRequest/WithEvidence, `SqlitePageQueries.cs:31-35`). If a stored snapshot carries it anyway, `RequestStoreConsistency.cs:24` refuses to open the store. All internal write paths read through the unprojected `QueryTransactions.Request` (`CompletionTransactions.cs:21`, `RequestStateTransactions.cs:11`, `CancellationTransactions.cs:19`, `UnknownTransactions.cs:20`), so a projected copy cannot be written back. This is the right choice.

**Double validation.** Sink (`DispatchObservationSink.cs:53-56`) and store (`ObservationTransactions.cs:21-24`) check the same things. The SessionEstablished pattern already did this before the patch. `FrameworkInvocation.Trustworthy` duplicates `HasUnconfirmedObservations` (`RequestDispatcher.cs:122`). Both checks can only downgrade a result to Unknown, so they cannot contradict each other. They do not form a second authority. FrameworkInvocation also adds two checks the sink lacks: the count limit and refusal after Close.

## Findings

**P2-1: the new branch condition in Common is pinned only by Framework.** The condition (`DispatchTermination.cs:48`) lives in Common, but the only test that drives it is Framework's terminal-race test (tdd.md: "the existing terminal-race case then exposed…"). No Common or contract test fixes the other side: an operation-aware provider with `ContinuationAvailable=null` and `Binding=null` must still reopen. `InteractionRecoveryTests` tests only the provider method, not the branch. Narrow it with a two-sided unit test on DispatchTermination, or move the decision into the provider.

**P2-2: `ExecutionEvidence.InvocationPrepared=false` mixes up "not prepared" with "this provider never reports it".** Only Framework emits InvocationPrepared. For Conversations and Interactions, Evidence is non-null after a reservation, but the flag is always false. A host that reads the flag as "not invoked" draws a wrong conclusion. Third-party `ISessionQueryStore` implementations do not project Evidence (example: the test fixture `ControlledQueryStore`). For them, null does not mean "no dispatch reserved" as the doc comment on `RequestSnapshot.cs` says. Narrow the doc to the provider and store that support the flag, or make the value tri-state.

**P2-3: the public contract document lags behind the public surface.** The patch adds these public members: FrameworkExecutionOutcome, FrameworkInvocation (TryGet, three Observe methods, three IDs), ExecutionEvidence, `RequestSnapshot.Evidence`, two `ProviderObservationKind` members, and `QualifiedOutcomes` and `MaximumObservations` in FrameworkAgentOptions. `contracts/public.md` lists none of them by name, and the patch documents them only in research.md and the ADR. The new enum members change behavior for third-party `ISessionStore` implementations: a store that rejects unknown kinds now turns *every* Framework dispatch into NotStarted, because InvocationPrepared is always committed first. The format bump hides this for SQLite only.

**P3-1: OperationReference is last-writer-wins, native session is first-writer-wins.** The sink accepts any nonblank OperationAccepted (`DispatchObservationSink.cs:57`), and the store overwrites it (`ObservationTransactions.cs:18`). This behavior predates the patch, but the agent can now trigger it through `ObserveOperationAcceptedAsync`, and the value becomes public evidence. Fix the first value, as the patch already does for NativeSessionObserved.

**P3-2: the cancellation read falls back to the unprojected snapshot.** `SqliteQueryStore.cs` uses `PublicRequest(...) ?? recorded`. If the row has vanished, this silently returns a snapshot without evidence. It should fail closed instead.

**P3-3: FrameworkInvocation holds two concepts.** It combines the public observation port with an internal gate (count, closed, pending, failed, Trustworthy) and the options factory. codemetr reports 0 violations, but the gate is a second, adapter-local "unconfirmed" tracker parallel to the sink's. Extracting an internal gate type would keep one concept per type.

**P3-4: validation message and configuration surface.** `MaximumObservations < 0` is rejected (`FrameworkSessionProvider.cs:18`), so 0 is allowed, but the exception text still says "positive state limit". It is also unclear that this needs to be a public option at all. The spec only requires the count to be bounded, which an internal constant would satisfy.

## Weaknesses of my own position

- If the Serialization behavior were reverted, the change would erase confirmed output, which `data-model.md:7`/`spec.md:12` forbid. The old test pinned the defect.
- Reverting DispatchTermination would turn every confirmed Framework end in the cancellation race into Unknown, because Framework has no `IExecutionRecoveryProvider`. That contradicts US3, which requires known ends to be reported.
- Without `RequestSnapshot.Evidence` or an equivalent read path, `spec.md:23` (native identity readable after restart) would have no implementation. A separate query method on `ISessionQueryStore` would break third parties more than an init property does.
- Most of my P2/P3 items are about documentation and test sharpness. I could not show any path in which today's code weakens a recovery guarantee.

Duration: about 3 minutes wall clock (18:37:30Z to 18:40Z, measured with `date -u`). About 22 tool calls.
