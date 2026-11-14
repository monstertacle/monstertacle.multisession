# Tasks

- [X] T001 Record existing-code deep dive, scope, design and consistency review.
- [X] T002 Add and execute failing routing checks in tests/quality/Test-RuntimeFramework.Tests.ps1 (FR-002).
- [X] T003 Add matching TFMs and SDK; regenerate all lock files (FR-001/005).
- [X] T004 Select every test/child runtime in scripts/Invoke-Quality.ps1 and ValidationHostBuild.cs (FR-002).
- [X] T005 Update package layout and all consumers in eng/ for both runtimes (FR-003).
- [X] T006 Extend native CI, setup and consumer guidance (FR-004/005).
- [X] T007 Run Linux and Windows quality, metrics, audit, consumers and publish probes; record evidence (FR-001–005).

Sequential implementation; heavy checks run one at a time. All five requirements
have executable tasks. No ambiguities, unmapped tasks or Constitution conflicts were
found during pre-implementation consistency analysis. Native Windows evidence is a
required CI result, never inferred from cross-target compilation on Linux.
