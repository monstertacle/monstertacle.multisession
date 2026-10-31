# Migration data model

The existing SQLite schema, request IDs, dispatch IDs, reservation model and ProviderBinding shape remain unchanged.

## Framework continuation

Version1 binding: AdapterId is explicit host configuration; SessionReference is the context SessionId in N format and remains stable. ContinuationState is a bounded JSON envelope containing AgentId, SessionId and agent-serialized JSON State. AgentId and SessionId must match the factory/context before any invocation; incompatible, missing or oversized state is not repaired. Serialized state is sensitive application state, not a diagnostic field. Host implementations must not serialize credentials. Each turn restores a separate AgentSession; host owns factory/agent resource lifetime.

## Evidence

EvidenceKind: Source, Rule, Binding, Verdict, RuleDecision, Artifact. Source and Rule are immutable input revisions. Every other node needs a receipt approved by a caller-supplied validator for its exact revision and full dependency fingerprint. IDs/revisions are nonblank; IDs unique; dependencies refer to declared distinct nodes and form a DAG. An invalid graph fails before user code. Fingerprints use stable ordered dependency identities and fingerprints and include node identity, kind and revision.

EvidenceReceipt: node ID, fingerprint, artifact reference, durable request ID for effectful work. Duplicate receipts for an ID are rejected rather than selected arbitrarily. A receipt cannot establish execution certainty.

RenewalAttempt: completed receipt or blocker, explicit execution certainty and durable request ID. Unknown effects immediately stop renewal and retain their request reference for coordination; no retry. Validators are pure project checks and cannot dispatch effects. Effectful renewal must return a reference to work admitted by the existing durable runtime; retries query that same operation.

RenewalOptions: positive MaximumAttemptsPerNode and finite positive Timeout. EvidenceWorkflowResult: currently accepted receipts, count of newly verified receipts, blocked IDs and coordination reason. Acceptance is revalidated against each frozen graph; stale receipts are dropped and never counted as new progress. No logs/status timestamps enter fingerprints or progress. Unchanged blockers consume the configured per-node attempt budget. Dependencies blocked by an unresolved predecessor are never renewed.
