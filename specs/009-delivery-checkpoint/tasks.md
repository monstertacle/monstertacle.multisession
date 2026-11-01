# Tasks

- [x] T001 Deep dive and architecture analysis (research.md); feature/ADR ids coordinated (009, ADR 0007).
- [x] T002 Fixture harness running the hash-verified current Listener on private copies; Listener-captured dump committed.
- [x] T003 Test project and fixture rebuild; Red/Green for open, identity, format and corruption refusal.
- [x] T004 Red/Green for transitions, bounds, cancellation and crash conversion.
- [x] T005 Red/Green for private access and exclusive lease (Linux locally, Windows in CI).
- [x] T006 Red/Green for attempt grants, resend authorization, route moves (atomicity, idempotency, uniqueness, capacity).
- [x] T007 Registration: solution, coverage scope, quality suites, package layout (Red: eight packages refused before change).
- [x] T008 Isolated package-only consumer script and CI step.
- [ ] T009 Local capped quality, Codemetr; PR with native Linux/Windows CI.
- [ ] T010 Two contrary implementation reviews on one frozen candidate; dispositions in evidence/review.md.
- [ ] T011 Version/merge coordination with the legacy-removal branch; handoff manifest (package/DLL/source hashes).

T003–T008 (2026-11-01): evidence in `evidence/tdd.md`; 94 behavior tests green on Linux; Windows quality job ran the
delivery tests natively (74/74 at d5088cd). T009: PR monstertacle/monstertacle.multisession#10, CI for the final head is
recorded in `evidence/handoff.md`. T010: reviews and dispositions in `evidence/review.md`. T011: merge order and shared
version are decided by the supervisor and the version owner of PR #11 (current-version-only, 0.2.0-alpha.5); a local trial
merge of #11 into this branch was clean (README only) and passed restore, build, delivery tests, layout and consumer.

Open follow-up owned outside this feature (not implemented here by authorization):
- F-009-1 (owner: product integration actor): prove the package against a read-only copy of the live Listener checkpoint
  and replace Store/Preparation*/DeliveryReplay*/DeliveryRebind* in SharpClaw with the package after the no-legacy work;
  acceptance: current host black-box suites green on the package-based Listener, identical keys/receipts on the copy.
