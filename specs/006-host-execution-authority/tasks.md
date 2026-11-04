# Tasks

- [x] T001 Record current source, callers, ownership and joint contrary review in research.md.
- [x] T002 Add behaviorless identity/dispatch/outcome/observation signatures and semantic Red tests in tests/Monstertacle.Multisession.UnitTests/ and IntegrationTests/.
- [x] T003 Implement canonical supplied IDs, exact submission fingerprint and duplicate refusal through Common/Cqrs and facades.
- [x] T004 Implement persisted controlled dispatch, atomic target-head reservation and queued-preserving shutdown in Common/RuntimeLifecycle.cs and Storage.Sqlite/.
- [x] T005 Implement qualified typed CLR outcomes and awaited invocation/native observations in AgentFramework/ and Common/Requests/.
- [x] T006 Qualify restart, late/malformed result, capacity/continuation independence and actual prior-reader refusal in IntegrationTests/.
- [x] T007 Run full static/native coverage/audit/package gates and bind artifacts under evidence/; produce source/hash-bound local alpha.3 packages.
- [x] T008 Consume alpha.3 in the isolated SharpClaw feature clone and record exact producer/source/package proof; preserve live work.

Partial milestones: identity/dispatch semantic Reds and canonical ID implementation are complete; T002 still owns outcome/observation Reds, and T003 still owns exact recovered-submission fingerprints. T007 is not complete: local Linux evidence is in evidence/qualification.json; native Windows and package proof remain pending. No feature completion or live cutover is claimed.

Consumer dependency for T008: active SharpClaw feature004 verified-work-control belongs to the concurrent session. Host proposal feature005 is deferred until coordination and reconciliation on that source. Continue T002/T003/T005–T007 in this SDK clone; do not implement SharpClaw work/progress authority in parallel.

Current ownership: explicit complete quiescent handoff accepted for SharpClaw server/client as well; the integration maintainer now owns all three repositories. Original84-file server patch and accepted design are preserved. Continue SDK feature006 and SharpClaw feature004 in their existing isolated clones, with heavy tests serialized. Fingerprint T003 is complete; full local Linux results are in evidence/fingerprint-qualification.json. T005/T006/T007/T008 still block overall completion.

## Follow-up (owned by the integration maintainer)

- [x] T009 Distinct session block reason for an explicitly unavailable framework continuation (today ProviderUnavailable), plus a consumer test with a real ChatClientAgent and recording IChatClient proving FrameworkInvocation never reaches chat options or the wire. Acceptance: semantic Red then Green; full native gates; host switch impact documented. Origin: review-r2 change P2-4/P3. Not a blocker for alpha.3 consumption; must precede a non-alpha release.
  Status 2026-11-03 (T009 stays open until its review is accepted and the change is merged; the checkbox is not a completion record):
  - Non-leak half, behaviour change, implemented with Red then Green: on `4b533c9`, `UnitTests/FrameworkChatClientIsolationTests` failed with the invocation key in chat options, because a real ChatClientAgent copies run-option additional properties into chat options; a measurement variant showed the key in the serialized request. It passed after the context moved to internal typed run options; two mutations were detected (evidence/t009-nonleak.md).
  - Block-reason half, decided by root choice C: document the existing contract instead of adding a block reason, a derived cause property, an API or a persisted format change. The documentation is in contracts/public.md and the decision in evidence/t009-block-reason-decision.md. Because choice C changes no behaviour, its acceptance is not a semantic Red: `IntegrationTests/ProviderBlockProvenanceTests` is a regression pin that passes on the unmodified implementation, and a recorded mutant of the revalidation rule made it fail (same decision file). The literal request for a distinct block reason is therefore not implemented, by decision.
  - Open for T009: accepted review, merge, full native gates on the merged head and the host switch impact record.
  - Release 2026-11-04: pull request #22 is merged; the `main` producer run 41738827195 at `67a7de9` passed all native gates, and the change is published as 0.2.0-alpha.9 (evidence/publication-receipt-0.2.0-alpha.9.json, evidence/publication-independent-check-0.2.0-alpha.9.txt). Still open for T009: the host switch impact record; consuming 0.2.0-alpha.9 in SharpClaw is a separate host task.
  - Host switch impact 2026-11-04: assigned to the accepted host consumption task MT-A9-HOST-01 (owner root, executed as SharpClaw spec 017-multisession-alpha9-consumption). T009 stays open until that consumption is accepted.
  - Closure 2026-11-04 (root acceptance):
    - Non-leak half: released in 0.2.0-alpha.9 (pull request #22, `main` producer run 41738827195 at `67a7de9`, publish run
      41742503760).
    - Block-reason half: disposed by root choice C, which documents the existing contract with no new block reason, cause property,
      API or format. The original request for a distinct reason is therefore not implemented, by decision.
    - Host switch impact: accepted with SharpClaw pull request #96 (spec 017, task origin MT-A9-HOST-01), normally merged as
      `c7ceee4` from the qualified head `344e852`. It consumes 0.2.0-alpha.9 from nuget.org for all six resolved packages, with
      250 lock entries canonical. The host's four `FrameworkInvocation.TryGet` call sites pass through the packaged adapter. The
      .NET gates give the same results as the base, including a standing FrameworkRuntime failure tracked as spec 017 T008, and the
      nine host Python suites pass (spec 017 T005).
    - This closes the source task. It is not a deployment, a live host change or a stable-release claim; the non-alpha release
      matrix remains open in the native-reliability follow-ups.

T005 progress (2026-10-31): typed outcomes, invocation context, preparation versus acceptance and request-linked native evidence implemented with observed semantic Reds (evidence/tdd.md) and contrary review r2 dispositions (evidence/review-r2/). T006 old-reader refusal is implemented in package smoke; restart/late result cases are in FrameworkEvidenceTests. T007 needs the final full gate, GitHub CI and alpha.3 feed binding.

T007 (2026-10-31): GitHub run 41231219057 for 94011f7 passed Linux/Windows quality (97.9511/97.9318 percent) and both canonical consumers including actual prior-reader refusal; canonical alpha.3 packages bound in evidence/alpha3-feed.json. T008 (SharpClaw consumption) remains open and follows SharpClaw feature004.

T008 (2026-10-31): SharpClaw branch feature/004-verified-work-control 745298b consumes the canonical alpha.3 packages of 94011f7 (hashes in evidence/alpha3-feed.json) through tests/MultisessionNuGet with locked restore, isolated cache and audit: caller GUIDs, host-controlled targeted dispatch, framework native-session evidence, typed known failure, duplicate refusal, queued-preserving stop and durable reopen passed. No live work was touched. Runtime migration of SharpClaw's execution boundary is SharpClaw feature005, not this SDK feature; T009 stays open as owned follow-up.
