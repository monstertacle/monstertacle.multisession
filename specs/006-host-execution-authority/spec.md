# Feature: Host-directed execution authority

Status: in_progress; identity and dispatch implementation under local qualification; typed outcomes and native observations pending.
Owner: primary integration agent.

## User scenarios

US1 (P1): a host persists its session and request GUIDs before admission. The canonical commands use exactly those IDs. Duplicate IDs reject before another open or execution; lost receipts are read by the original ID. Empty IDs fail before effects. Existing immutable session context and submission fingerprints distinguish unrelated or changed work.

US2 (P1): a host starts a persisted host-controlled runtime to inspect/reconcile state without queued execution. It dispatches only an explicitly named eligible FIFO head. Another queued head is never substituted. Shutdown preserves unstarted queued work in this mode. Automatic dispatch remains a legitimate standalone mode, not a second host execution path. Store mode is immutable; mismatched and historical formats refuse before mutation/dispatch.

US3 (P1): qualified framework agents return typed native outcome facts for success, known failure, not-started and Unknown, independently of continuation and complete output. Plain model text/JSON cannot select the control contract. Generic exceptions remain Unknown. Invalid typed metadata fails closed without fallback. Confirmed native end followed by serialization failure preserves end and captured output while invalidating continuation.

US4 (P1): framework invocation options expose a typed awaited observation sink. Invocation preparation is distinguished from actual external operation acceptance. Native session observation is separate from SDK session identity, persists before continuing, and never authorizes resume. Failed/late observations cannot be overtaken by success.

## Acceptance

- Exact supplied IDs appear in public receipts, session snapshots, durable rows and restart queries. At most one external open/invocation per supplied identity; no automatic resubmission.
- Controlled startup and controlled shutdown invoke zero queued agents. Explicit dispatch of an ineligible/non-head/foreign ID invokes zero agents and preserves FIFO/reservations.
- Both modes and mode mismatch are tested on actual SQLite with native platform ownership. Actual prior package code rejects the new store format before any provider construction/invocation.
- Known failed process outcomes remain ConfirmedEnded and release appropriate capacity; partial output never becomes complete success. Invalid continuation blocks later turns, independently of known end.
- Typed outcomes require explicit qualification. Wrong CLR type, inconsistent states and background continuations reject; ordinary textual success remains qualified by the existing narrow contract.
- Native observation identity is available after process death/reopen as evidence linked to the original request. It is never silently installed as confirmed continuation.
- All own product libraries remain in the overall87-percent native Linux/Windows gate; static/tool pins and thresholds are unchanged. Local alpha.3 package consumption is isolated and source/hash-bound; public publication is unnecessary.

## Scope and breaking change

Canonical public creation/submission/targeted-dispatch contracts change; no compatibility aliases are required. New stores persist format2 and immutable dispatch policy; existing format1 stores are not reset or automatically migrated. No live runtime/session is touched. This is the explicit producer prerequisite for SharpClaw feature004; host source-work acceptance remains outside the SDK.
