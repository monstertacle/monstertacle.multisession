# Tasks

- [x] T001 Constitution 2.1.0: faithful English translation, principle VI, review obligation in process step 4, amendment record;
  stale 2.0.0 Sync Impact Report comment removed (it was marked for removal before commit).
- [x] T002 AGENTS.md translated; English rule added.
- [x] T003 README.md and docs/{operations,embedding,providers,cqrs}.md translated (translator output reviewed against the frozen
  originals before copying).
- [x] T004 Feature 001 core documents translated (spec, plan, tasks, research, data-model, quickstart, contracts, checklists).
- [x] T005 Archive notes beside the non-English hashed evidence of features 001 and 003; inventory with dispositions.
- [x] T006 Narrow opposed review of the constitution translation (normative meaning) and spot review of the large translations.
- [ ] T007 Required CI on the PR head; merge.

Process deviation (2026-11-01T21:02:13Z): the verified identifier/fact corrections to the staged English docs (outside the repository,
under the private staging directory) were applied by one temporary python3 replacement script, which constitution principle I
forbids for SDK work including temporary helper scripts. Nothing was executed in the SDK checkout; the corrected text is plain
replacements listed in inventory.md. Further SDK work uses shell tools or PowerShell only.

Translation (2026-11-01): two translators worked on frozen copies (digests in the private staging directory); every file keeps its
line count, headings, checkbox states, task and requirement IDs and code fences. Ambiguities and corrections:
translation-notes.md. Corrected facts found during translation: the stale license sentence and provider type/package names in
docs/embedding.md and docs/providers.md (now the current public types); the embedding example sets the required `Endpoint`.

Constitution review (opposed roles on frozen DE/EN copies, report digests in the staging directory; challenger accept-with-fixes,
~3 min/~6 calls; defender accept, ~3 min/~8 calls; no P1). Fixed: fakes MUST NOT be passed off as real acceptance (F1, was
"nor may fakes simulate them"); "domain-justified runtime strategy" (F2); "MUST NOT be a project prerequisite" (F3); "access paths"
(F4); "materially expanded requirements" (F5); amendment record names affected flows, migration plan, approval basis and the
review (F6, F7, F15); per-file disposition includes archives and a completion criterion (F8, F9); messages include exception and
log messages, script/CI messages limited to project-emitted ones (F10, defender F7); conversation scope wording (F12); AGENTS.md
exemption narrowed to the constitution's (F11, defender F13). Kept: no rule on renaming already published identifiers (F13: the
probe flags no C# source except two Unicode test-data literals; it cannot see German words without umlauts, so identifiers
were not audited beyond it); the step-4 review sentence stays a check of new and changed content (F14).
