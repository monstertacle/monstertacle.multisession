# Agent Framework execution and evidence renewal

Status: accepted for implementation; release qualification pending.

Context: hosts need provider-neutral agents and bounded evidence renewal without losing durable request certainty. Framework checkpoints do not transact with external effects.

Decision: integrate Microsoft Agent Framework1.27.0 behind the existing ISessionProvider seam. Keep CQRS/SQLite/reservations/recovery authoritative. Restore isolated agent sessions from versioned confirmed continuation. Add a typed renewal workflow that only reads already durably associated requests and validates revision-bound receipts. Project validators own substantive judgments. No workflow admission, automatic replay, second durable ledger or vendor-name dispatch.

Alternatives: replacing the durable core would discard current crash/ownership guarantees; another custom agent loop would duplicate framework responsibilities; DurableTask adds an unnecessary second scheduler here.

Consequences: exact package/type migration is documented; wire IDs are retained. Narrow final response qualification rejects unsupported shapes. Generic remote inspection/cancellation remains unsupported and uncertain sessions can stay blocked. Native CI, complete coverage, package-only consumers and host qualification are mandatory before runtime rollout.
