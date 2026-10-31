# Tasks

- [x] T001 Record current source, callers, ownership and joint contrary review in research.md.
- [x] T002 Add behaviorless identity/dispatch/outcome/observation signatures and semantic Red tests in tests/Monstertacle.Multisession.UnitTests/ and IntegrationTests/.
- [x] T003 Implement canonical supplied IDs, exact submission fingerprint and duplicate refusal through Common/Cqrs and facades.
- [x] T004 Implement persisted controlled dispatch, atomic target-head reservation and queued-preserving shutdown in Common/RuntimeLifecycle.cs and Storage.Sqlite/.
- [x] T005 Implement qualified typed CLR outcomes and awaited invocation/native observations in AgentFramework/ and Common/Requests/.
- [x] T006 Qualify restart, late/malformed result, capacity/continuation independence and actual prior-reader refusal in IntegrationTests/.
- [x] T007 Run full static/native coverage/audit/package gates and bind artifacts under evidence/; produce source/hash-bound local alpha.3 packages.
- [ ] T008 Consume alpha.3 in the isolated SharpClaw feature clone and record exact producer/source/package proof; preserve live work.

Partial milestones: identity/dispatch semantic Reds and canonical ID implementation are complete; T002 still owns outcome/observation Reds, and T003 still owns exact recovered-submission fingerprints. T007 is not complete: local Linux evidence is in evidence/qualification.json; native Windows and package proof remain pending. No feature completion or live cutover is claimed.

Consumer dependency for T008: active SharpClaw feature004 verified-work-control belongs to the concurrent session. Host proposal feature005 is deferred until coordination and reconciliation on that source. Continue T002/T003/T005–T007 in this SDK clone; do not implement SharpClaw work/progress authority in parallel.

Current ownership: explicit complete quiescent handoff accepted for SharpClaw server/client as well; the integration maintainer now owns all three repositories. Original84-file server patch and accepted design are preserved. Continue SDK feature006 and SharpClaw feature004 in their existing isolated clones, with heavy tests serialized. Fingerprint T003 is complete; full local Linux results are in evidence/fingerprint-qualification.json. T005/T006/T007/T008 still block overall completion.

## Follow-up (owned by the integration maintainer)

- [ ] T009 Distinct session block reason for an explicitly unavailable framework continuation (today ProviderUnavailable), plus a consumer test with a real ChatClientAgent and recording IChatClient proving FrameworkInvocation never reaches chat options or the wire. Acceptance: semantic Red then Green; full native gates; host switch impact documented. Origin: review-r2 change P2-4/P3. Not a blocker for alpha.3 consumption; must precede a non-alpha release.

T005 progress (2026-10-31): typed outcomes, invocation context, preparation versus acceptance and request-linked native evidence implemented with observed semantic Reds (evidence/tdd.md) and contrary review r2 dispositions (evidence/review-r2/). T006 old-reader refusal is implemented in package smoke; restart/late result cases are in FrameworkEvidenceTests. T007 needs the final full gate, GitHub CI and alpha.3 feed binding.

T007 (2026-10-31): GitHub run 41231219057 for 94011f7 passed Linux/Windows quality (97.9511/97.9318 percent) and both canonical consumers including actual prior-reader refusal; canonical alpha.3 packages bound in evidence/alpha3-feed.json. T008 (SharpClaw consumption) remains open and follows SharpClaw feature004.
