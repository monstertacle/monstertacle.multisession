# Feature: Opaque delivery checkpoint and atomic grant primitives

Status: package qualified as 0.2.0-alpha.6 (main 06bbbcc); host integration and activation open (F-009-1)
Owner: delivery package implementer (work `monstertacle-delivery-implementation-20261101`); host integration stays with the product actor.

## Authority and scope

The owner commissioned a bounded extraction of the durable single-channel delivery checkpoint from the SharpClaw Listener into
Monstertacle (owner request recorded 2026-11-01T11:40:14Z). Two opposed architecture reviews were completed; the source-checked
decision (recorded 2026-11-01T11:50:54Z, review digests `775cb8dc…cf21` and `464587f9…cc96`) selects the intersection: an opaque
current-format checkpoint plus typed atomic receipt/compare-and-swap primitives. The owner then required SharpClaw to be treated
as one of many possible consumers (2026-11-01T11:51:45Z). This feature is the binding requirement in the sense of the SDK
constitution (YAGNI: "current use case or binding requirement"); no second production consumer is claimed.

In scope: new package `Monstertacle.Multisession.Delivery`, its C# tests, solution/CI/package registration, an isolated
package-only consumer and this documentation. Out of scope (rejected, not deferred): feeds/sources, destination interfaces,
send-attempt runners, external sends or turn starts, timers/background retries, lifecycle/subagent outboxes, a project event feed,
message bus, host policy (allowances, configuration digests, routing decisions, readiness), any change to session runtime
contracts, and any converter, importer or alternate reader for other checkpoint formats.

## User scenarios

US1 (P1) Durable single-channel custody: a host opens one checkpoint per channel, stages one opaque canonical item at the next
position, marks it Sending immediately before its external effect, records Accepted only with a positive acknowledgement for the
exact stored bytes, and after a crash finds Sending converted to Uncertain. The host never gets automatic resend authority.

US2 (P1) Current custody without migration: a checkpoint written by the current SharpClaw Listener (2c4a8af), including the
`preparation_rearms`, `delivery_replays` and `delivery_rebinds` tables, opens unchanged; its pending bytes, position, attempts,
acknowledgement and every stored receipt are returned byte-for-byte and keep governing idempotency and unique consumption.

US3 (P1) Host-authorized grants, library-enforced invariants: an operator-authorized host records (a) further attempts for a
pending item, (b) the single resend allowance for an Uncertain item, or (c) a route move for a never-sent item. Each receipt
insert and its state transition commit in one library-owned SQLite transaction against an exact expectation.

US4 (P2) Host neutrality: an independent console consumer restores only the packed nupkg, uses its own payload and policy, and
observes the same results and refusals; no SharpClaw assembly or vocabulary is required.

## Requirements

- FR-001 Package `Monstertacle.Multisession.Delivery` (net10.0, MIT) depends only on `Microsoft.Data.Sqlite`; no reference to
  Common, AgentFramework, Linux/Windows lease packages, providers, SharpClaw or Microsoft Agent Framework.
- FR-002 Public vocabulary is host-neutral: channel (consumer, source, route), position, pending item, phase, acknowledgement,
  expectation, attempt grant, resend authorization, route move. Current on-disk names (`instance`, `project`, `cursor`,
  `preparation_rearms`, …) are private.
- FR-003 Current format, retained canonically: tables, column names, `CREATE TABLE` text, phase strings, `schema = 1`, the
  `<state>.lock` sibling lease, WAL and `synchronous=FULL`. Open verifies the stored `CREATE TABLE` text of every present table
  exactly; any other structure, schema value, phase string, phase/pending combination or non-UTF-8 pending text is refused as
  corrupt or unsupported. There is no converter, importer, upgrade or alternate reader.
- FR-004 Owner open (`OpenAsync`) creates the checkpoint if missing with the channel identity and initial position, applies WAL
  and `synchronous=FULL`, refuses another identity, and converts Sending to Uncertain. Inspection open (`OpenExistingAsync`)
  refuses a missing file, creates nothing and leaves Sending unchanged. Both hold an exclusive lease on `<state>.lock` for the
  object lifetime; a refused open releases lease and connection immediately.
- FR-005 Private state: on Unix the owner open creates the state directory and sets it to owner-only (0700), creates the
  database and lease files owner-only (0600); on Windows it applies a protected owner-only access rule to the state directory,
  so new files inherit it. The library never weakens existing permissions.
- FR-006 Transitions are compare-and-swap updates of the single row: Stage only when nothing is pending and position equals
  stored position + 1 (attempts reset to 0); MarkSending only from Pending (attempts + 1); RecordPreparationFailure only in
  Pending (attempts + 1); MarkUncertain only from Sending; Accept only from Sending with byte-identical payload (position + 1,
  pending cleared, attempts 0, acknowledgement stored). Attempt counters never overflow.
- FR-007 Payload is opaque canonical UTF-8 supplied by the host: non-empty, strictly valid UTF-8, no NUL, at most
  `MaximumPayloadBytes` (default 262 144, bounds 1 … 16 777 216). Acknowledgements are 1 … 1024 characters and not blank.
  The library never parses, logs or reserializes payloads, acknowledgements or receipts.
- FR-008 `CheckpointExpectation` (route, position, SHA-256 lowercase hex digest of the stored pending bytes, phase, attempts)
  is the exact expected key of every grant; the digest is computed by the library from the stored bytes.
- FR-009 Attempt grant: expectation phase Pending; exact match; attempts strictly greater than every earlier grant for the same
  context, route, position, item and digest; one row in `preparation_rearms`; the checkpoint row is not changed.
- FR-010 Resend authorization: expectation phase Uncertain with 1 ≤ attempts < Int32.MaxValue; at most one authorization per
  item ever (`pending_id` uniqueness, also for a fresh request id and after restart); receipt insert plus Uncertain → Sending
  (attempts + 1) in one transaction; the external send remains the host's.
- FR-011 Route move: expectation phase Pending with an item; new route differs; receipt insert plus route compare-and-swap on
  route, position, attempts, phase and exact pending bytes in one transaction; afterwards only the new channel identity opens.
- FR-012 Idempotency: a request id already stored returns the stored receipt text unchanged when its stored columns match the
  request; otherwise RequestConflict. Find operations read receipts by request id without creating tables.
- FR-013 Capacity: each grant table retains at most `MaximumRetainedGrants` (default and maximum 1024) rows; a full table refuses
  without eviction or mutation.
- FR-014 Typed refusals: `DeliveryRefusalException.Reason` (`DeliveryRefusal`); no host strings. Hosts map reasons to their own
  operator codes. Cancellation before commit throws `OperationCanceledException` and leaves state unchanged; it is never success.
- FR-015 Guarantees are limited to durable transitions: at most one automatic Sending per staged item and at most one explicitly
  authorized resend per item. Accepted means a positive acknowledgement supplied by the host's adapter, nothing more. There is no
  exactly-once guarantee. Delivery uncertainty is unrelated to session execution recovery (`IRecovery`, `ExecutionCertainty`).
- FR-016 Linux and Windows are both supported targets with native CI tests for lease exclusivity and private access, and the
  package is inside the 87 percent product coverage scope on both platforms; Codemetr full check stays clean.
- FR-017 Packaging: the package joins the existing package layout check with the root version, MIT expression, copyright and
  LICENSE; an isolated consumer restores the exact nupkg from a private feed with a lock file (no ProjectReference).

## Success criteria

- SC-001 Every FR has a C# behavior test observed Red before Green (evidence/tdd.md).
- SC-002 Fixture tests rebuild the current Listener checkpoints from the Listener-captured exact DDL and row dump and prove
  byte-identical pending bytes and receipts, unique consumption, idempotency and a route move on them.
- SC-003 Native Linux and Windows CI pass quality (coverage ≥ 87 percent), package layout, package smoke and the delivery consumer.
- SC-004 Two contrary implementation reviews on one frozen candidate with source-bound dispositions (evidence/review.md).

## Amendment to earlier wording

The phrase "exactly once" in the host's earlier rebind specification described one never-sent test event. For this package the
guarantee is FR-015; historical evidence is not rewritten.
