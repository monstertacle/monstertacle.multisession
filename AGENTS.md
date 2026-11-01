# Binding project rules

- Do not use Python for product code, tests, build/CI scripts, code generation or
  agent-assisted project work, not even for temporary helper scripts or one-off commands.
  Use C#/.NET and, where needed, PowerShell or shell scripts; do not introduce a Python
  runtime as a project prerequisite.
- Write all maintained project content in English: code identifiers and comments, messages,
  tests, README, docs, examples, specs, plans, tasks, ADRs and these instructions
  (constitution principle VI). Only data stays as it is: externally fixed identifiers, test data
  that exercises language or Unicode handling, verbatim quotations and proper names; explanatory
  text around them is English. Archived hashed evidence, released artifacts and commit history are
  not rewritten; they get an English note. Existing non-English files are migrated, not
  grandfathered. Conversation with the owner may use any language.
- Before implementation, read `.specify/memory/constitution.md`, the feature plan and its
  `tasks.md`. Follow TDD with an actually observed domain Red before Green.
- Before every extension, first examine the current existing code through a code deep dive and
  architecture analysis: affected modules, relevant callers/dependencies, responsibilities,
  contracts, data/control flows, state/persistence and tests. Assess reuse, extension points and
  compatibility/regression risks. Before design/implementation, document the source state/source
  hash, concrete file/symbol evidence, findings, open questions, architecture decision and test
  plan in the change evidence. Do not implement without this evidence or with unresolved
  implementation-relevant architecture questions. Small extensions also need the analysis;
  re-check old analyses against the current code and scope. Name missing existing code
  explicitly; do not pretend an examination.
- For C# changes and refactorings, load and follow the project-local skill `codemetr-guard`
  at `.opencode/skills/codemetr-guard/SKILL.md`. If the client does not list the skill yet,
  read the file directly.
- From the project root, run the complete guard:
  `dotnet tool run codemetr-csharp -- check --format json`.
  It covers the project's own C# classes including tests and ValidationHost. Check again after
  refactoring. Missing tools, input errors and missing measurement scope are no PASS.
- Do not change `codemetr.json` and tool pins silently; do not create a debt baseline,
  exclusions or relaxed thresholds to hide findings.
- Before release, prove at least 87 % line coverage of all of the project's own production code,
  not only changed lines. Windows and Linux CI must each enforce the gate. Include the native
  platform library and all shared product libraries in the measurement; report the other
  platform separately and check it mandatorily in the matching native CI run. Test code does not
  count toward the coverage denominator.
- Skills and local successes do not replace required CI/release evidence.
  Do not claim acceptance with zero measured classes or tests that did not run.
