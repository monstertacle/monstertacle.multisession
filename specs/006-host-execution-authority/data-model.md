# Data model

SessionId and RequestId are nonempty caller-selected GUIDs, persisted by the caller before command admission. SDK storage still owns acceptance and uniqueness. Request payload remains separate from request identity; a canonical submission fingerprint binds recovered records to the original payload.

DispatchMode is Automatic or HostControlled and is persisted as immutable format2 metadata. Explicit dispatch requires the specified eligible FIFO head; no substitution. HostControlled stop preserves queued input and drains tracked active operations before releasing ownership. Format1 or mode mismatch refuses without migration.

FrameworkExecutionOutcome is an explicitly qualified CLR result: local terminal state, execution certainty, actual output, output completeness, continuation availability and stable error code. Wrong CLR type or invalid combinations fail closed. It never comes from model-output JSON. Confirmed end/output survive serializer failure; continuation becomes unavailable.

FrameworkInvocation is transient typed AgentRunOptions data for the current request/dispatch and awaited observation sink. InvocationPrepared proves only durable invocation intent; OperationAccepted is actual external operation evidence. NativeSessionObserved records a dispatch/request-linked external identity, separate from SDK Binding.SessionReference and confirmed continuation. Observations never authorize replay.
