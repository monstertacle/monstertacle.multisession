# Full host integration follow-up

Owner: Alexander Bleissem
Status: ready for contract design; live execution awaits explicit scope and credentials
Origin: feature004 source analysis and opposed architecture reviews; the offline
preallocated-ID transport proof does not cover every current SharpClaw backend.

## Bounded work

Integrate one explicitly selected current SharpClaw backend at the host composition
boundary. Preserve host policy and SDK neutrality. Do not introduce a generic
backend kernel or promise migration of existing sessions by default.

## Next steps and dependencies

1. Select the concrete backend and freeze its current identity, observation,
   completion and cancellation contract. Inspect the actual late-ID path before
   designing any asynchronous observation bridge.
2. Add executable negative contract cases for delayed identity, unacknowledged
   observations, cancellation during startup/execution, worker crash and lost
   completion receipt. Use the existing external driver for measurements.
3. Implement the smallest host-owned isolated composition after observed behavior
   gaps. Bind immutable workspace/model/native identity and prevent implicit replay.
4. Run current-source first/follow-up/reopen/concurrency/cleanup cases and native
   checks for every platform actually claimed. Run live cases only after the
   selected provider, cost and credential scope is explicitly authorized.

Dependencies: accepted feature003 native reliability and quality checks, selected
backend contract, and an explicit decision on any existing-session migration.

## Acceptance

For the selected backend, first and follow-up requests survive host/store reopen
with unchanged bindings; concurrent cleanup cannot affect another session;
every observation-dependent effect follows durable acknowledgement; unsupported
actions fail explicitly; worker loss never invents external completion or causes
automatic replay. Live and native evidence must identify exact source and packages.
Absent evidence remains a release blocker for the corresponding integration claim.
