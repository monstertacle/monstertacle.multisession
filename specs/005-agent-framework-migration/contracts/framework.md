# Framework integration contracts

`FrameworkSessionProvider` implements ISessionProvider; the host supplies a stable adapter ID, execution kind and context-to-AIAgent factory. No names select behavior. A host declares whether a workspace is required and configures finite session-operation timeout and maximum serialized state bytes. Generic AIAgent serialization is not a promise of arbitrary vendor-native recovery.

Capabilities: selected execution kind only; inspection=false; remote cancellation=false; operation-aware recovery=true. Unsupported inspection/cancellation returns a typed UnsupportedProviderAction with Unknown certainty and performs no agent call. The operation observation identifies the durable DispatchId and is acknowledged before RunAsync; it is a local invocation intent, never a claimed remote operation receipt. Factory/restore/acknowledgement failure before RunAsync is NotStarted; after RunAsync starts without terminal evidence failure is Unknown. Cancellation before RunAsync throws/returns not-started; after invocation is Unknown. No retry or fallback. Runtime keeps custody on Unknown.

Only explicit Stop finish reason and supported text-only final response counts as complete by default. Missing/other finish reason, approvals, function/tool content or nontext is failure; no partial text is returned as complete. Successful terminal execution updates serialized continuation, even on output limit rejection. Serialization/state-size failure after known terminal result blocks further turns. Nonterminal response is Unknown and has no new continuation. Host agents must honor cancellation cooperatively; outer runtime can stop waiting without declaring remote end.

`EvidenceWorkflow` executes a typed framework graph with deterministic executors. Validator receives node and receipt; true is an explicit project attestation, not an agent claim. Renewal callbacks accept revision-bound task data and prior request reference. Effectful results require durable RequestId. Unknown result halts to coordination with request preserved. Framing this as a workflow does not permit resubmission of an unresolved durable request. Caller persists the returned receipt set before a subsequent round. Public workflow execution does not expose checkpoint replay as authorization to repeat effects.

## Architecture dispositions

Renewal takes `IRequests` only to read already durably associated RequestId/SessionId pairs from EvidenceWorkItem. No renewal callback can submit work because this API exposes no execution callback. Missing/ambiguous association is coordination; callers recover prior admission instead of substituting a request. Restart uses the same associations and receipts.

Accepted final shape: Stop reason, no background continuation token, final Assistant message with nonempty text-only content. Only that final message contributes output; earlier completed tool/intermediate messages do not. Other final shapes are Unknown, not confirmed success. Serialization failure after this terminal shape returns the unchanged binding with ContinuationAvailable=false, preserving known finality even during a cancellation race while blocking further turns.

## Validation and cancellation refinement

Project validation uses `Func<EvidenceNode, EvidenceReceipt, CancellationToken, ValueTask<bool>>`. The round deadline starts before receipt preparation. Callback synchronous prefixes and asynchronous waits run outside the calling thread under bounded waiting; cancellation is passed through and late results cannot publish acceptance. The library cannot physically stop uncooperative host code. Validators must remain pure, cancellation-aware and safe for independent callback scheduling.

Pending request observations wait the positive finite ObservationInterval between attempts. Interval, attempt budget and overall timeout are explicit options; no file activity resets them. Cancellation/deadline before agent invocation preserves an otherwise valid continuation. Invalid state and uncertain execution continue to block.
