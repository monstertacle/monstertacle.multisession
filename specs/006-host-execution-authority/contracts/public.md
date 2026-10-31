# Public contract changes

Canonical ISessions creation takes caller SessionId; IRequests submission takes caller RequestId separately from SubmitRequest payload. CQRS commands carry the same IDs and facades forward them unchanged. Empty and duplicate IDs reject before effects; no auto replacement. Caller recovery queries exact IDs and compares immutable context/submission fingerprint.

SessionRuntime exposes a typed targeted-dispatch command in HostControlled mode. Automatic scheduling and explicit target dispatch share the same atomic store policy; incorrect mode/target cannot fall back to another queued request. Mode persists in format2 and is checked before recovery/start. Controlled shutdown preserves queued input.

The framework package adds its own qualified typed outcome and invocation-context contracts using public AIAgent/AgentSession/AgentRunOptions APIs. Microsoft Agent Framework remains an unmodified external NuGet dependency. No internal framework access, fork or upstream patch is required.

Typed outcome admission validates declared state/certainty/output/continuation consistency and pending/background flags before serialization. Native session observation commits separately from stable framework session binding. Captured output and known termination are not erased by missing continuation.

## Typed outcome, invocation and evidence surface (T005)

- `FrameworkAgentOptions.QualifiedOutcomes` (default false) and `MaximumObservations` (default 16, nonnegative; 0 forbids agent-reported observations).
- `FrameworkExecutionOutcome`: recognized only as the exact CLR `AgentResponse.RawRepresentation` of a qualified registration. A typed NotStarted after an acknowledged operation or native session observation is Unknown.
- `FrameworkInvocation.TryGet(AgentRunOptions?)`: transient context with `ObserveOperationAcceptedAsync` (once per invocation), `ObserveNativeSessionAsync` (first value fixed) and `ReportProgressAsync`; identities are nonblank and at most `MaximumReferenceCharacters` (512). Callbacks are awaited, deadline-bound and refused after the invocation returned.
- `ProviderObservationKind.InvocationPrepared` and `NativeSessionObserved` are new members. Only the framework adapter emits InvocationPrepared; for other adapters `ExecutionEvidence.InvocationPrepared` is false by construction and means only "not recorded". Third-party `ISessionStore` implementations must persist or explicitly refuse these kinds; refusal makes framework dispatches fail before invocation.
- `RequestSnapshot.Evidence` (`ExecutionEvidence`): public store reads, lists and cancellation results project it from the stored dispatch envelope; it is null before dispatch reservation. Stored snapshot copies are refused. Store implementers must project it on every public request read.
- A confirmed end whose continuation is unavailable blocks the session with the existing `SessionBlockReason.ProviderUnavailable`. Framework sessions have no operation-aware recovery provider, so the block persists until host-level replacement; a distinct block reason is recorded follow-up work (tasks.md T009), not part of this increment.
