# monstertacle.multisession Constitution

## Core Principles

### I. Mandatory C# and .NET standards

- Application code MUST be developed in C# on a supported .NET version.
  The concrete SDK version MUST be fixed before implementation starts.
- In `monstertacle.multisession`, Python MUST NOT be used for product code, tests,
  build/CI scripts, code generation or agent-assisted project work. C#/.NET and, where needed,
  PowerShell or shell scripts are to be used instead. The prohibition also applies to temporary
  helper scripts and one-off agent commands; a Python runtime MUST NOT be a project prerequisite.
- Code MUST follow the documented Microsoft C# conventions and the agreed
  .NET analyzer rules. Formatting and nullable reference types MUST be enabled.
- New compiler and analyzer warnings MUST be fixed before release.
  Suppressions require a local, reviewable justification.
- Asynchronous I/O MUST use async/await; blocking waits on tasks are prohibited in the
  application path. Resources MUST be released through defined lifecycles.
- Long-running operations and external calls MUST support cooperative cancellation.
  A passed CancellationToken MUST be forwarded to the supporting dependencies
  and evaluated regularly in the project's own loops.
- External calls MUST have finite, documented timeouts. For long-running flows, timeouts or a
  domain-justified runtime strategy MUST be defined. Cancellation MUST release resources and MUST NOT
  be reported as successful completion.

These rules make "best practices" verifiable instead of a mere declaration of intent.

### II. Simple, maintainable architecture

- Before every extension, the current existing code MUST be examined through a thorough
  code deep dive and an architecture analysis before the extension is designed and implemented.
  This also applies to small extensions and agent-assisted work; merely reading a plan or
  documentation is not sufficient. The affected modules and their relevant calling and called
  components, responsibilities, dependency directions, public contracts, data and control flows,
  state and persistence rules and existing tests are to be examined. Existing extension points,
  reusable domain rules, technical debt, compatibility and regression risks MUST be assessed.
  The depth of the examination MUST match the impact; small extensions are not exempt from
  the obligation.
- The analysis of the existing code MUST be documented before implementation with the
  requirement reference, the examined source state or source hash, concrete file/symbol evidence,
  findings, open questions, architectural consequences, the chosen extension point and the planned
  tests. Without this evidence, or with unresolved implementation-relevant architecture questions,
  implementation of the extension MUST NOT begin. Earlier analyses may only be reused after
  re-checking them against the current existing code and the concrete scope of the extension.
  If existing code is missing, this MUST be recorded explicitly together with the examined scaffold
  and existing contracts instead of claiming a code analysis.
- Classes and modules MUST have a nameable responsibility. Domain logic MUST stay separate from
  user interface, persistence and external integrations.
- Classes and interfaces MUST comply with the SOLID principles:
  - Single Responsibility: every class MUST have exactly one nameable domain responsibility
    and therefore one cohesive reason to change.
  - Open/Closed: existing extension requirements MUST be implemented through suitable interfaces
    or composition without having to change independent domain logic.
    This does not imply an obligation to add speculative extension points.
  - Liskov Substitution: subtypes MUST honor the contracts of their base types and be usable in
    their place without changed behavioral expectations.
  - Interface Segregation: interfaces MUST be tailored to their users; users MUST NOT be forced to
    depend on methods they do not need.
  - Dependency Inversion: domain logic MUST NOT depend on concrete infrastructure implementations;
    dependencies across this boundary MUST rely on abstractions.
- DRY (Don't Repeat Yourself) MUST apply to domain knowledge: the same business rule,
  validation rule or domain constant MUST NOT be maintained independently in several places.
  Shared rules MUST have one authoritative definition. Code that only looks similar but has a
  different domain meaning MUST NOT be coupled artificially.
- Anti-patterns MUST NOT be newly introduced. In particular, magic values, god classes,
  service locator and copied business rules are prohibited. Existing findings in the changed
  area MUST be fixed or approved as a time-limited exception.
- Magic values are numeric or string literals whose domain or technical meaning is not
  immediately recognizable. They MUST be replaced by named constants, enums, value objects or
  validated configuration. Obvious structural values such as the start index 0 of a loop do not
  need an artificial constant.
- Mutable external dependencies MUST be replaceable and testable in isolation.
  Global mutable state and hidden dependencies are to be avoided; necessary exceptions MUST be
  documented and approved in review.
- The multisession core and its public session operations MUST stay generic with respect to
  vendors, agent products and execution transports. Selection MUST happen through explicitly
  registered, polymorphic adapters. Provider, backend and model identifiers are technical
  configuration data, not a basis for vendor-specific if/switch branches in the core. Mapping
  identifiers to registered adapters at the composition edge is permitted.
- Protocol, authentication, native session/continuation rules and the interpretation of
  completion and failure signals MUST be encapsulated in the respective adapter.
  A further adapter for a new contract MUST be registrable without vendor special cases
  or changes to existing domain logic. Already supported compatible access paths and models MUST be
  selectable through configuration.
- Required capabilities and missing support MUST be represented through explicit contracts or
  typed results, not guessed from product names. Unsupported behavior MUST be rejected
  traceably; there MUST NOT be a silent fallback or pretended equivalence.
- A model API and a complete agent backend MUST NOT be equated.
  The generic contract MUST actually be able to represent the guaranteed agent session
  requirements: host-side workspace reference, stable opaque native identity, continuation,
  confirmed completion, cancellation and uncertain handover. Tool loops, context management and
  compaction stay with the external agent/provider backend; the library MUST NOT replace missing
  capabilities with its own agent runtime.
- Embeddability for a named target host MUST be planned against that host's actually examined
  execution and session contract and proven by suitable contract/consumer tests. An arbitrary
  fake or a model response alone does not prove an agent integration. The library MUST NOT depend
  on the target host through assemblies or source code copies; a shared abstraction is no promise
  to take over existing native sessions or to support all backends.
- KISS (Keep It Simple) MUST govern the choice of solution: the simplest solution that meets
  the current requirements and quality standards is to be chosen. Additional layers, design
  patterns or dependencies require a documented justification.
- YAGNI (You Aren't Gonna Need It) MUST be observed: abstractions and features require a current
  use case or a binding requirement. Speculative extension points and unused functionality MUST NOT
  be introduced.
- Architecture decisions with long-term impact MUST be documented with context, alternatives and
  consequences.

The goal is understandable changes and limited coupling, not a maximum number of layers.

### III. Codemetr check and controlled refactoring

- Every new or changed project-owned C# class MUST be checked before release with the official
  .NET CLI tool `Codemetr.CSharp.Tool` 0.1.0 from the project `codemetr/Codemetr.CSharp`,
  including test and host classes. The project-local skill `codemetr-guard`, derived from
  `codemetr/skills`, MUST accompany the agent-assisted check workflow; it replaces neither the tool
  nor the CI gate. Generated code and third-party code may only be excluded with a documented
  delimitation.
- Tool version, check scope, relevant results and the decision on findings MUST be recorded in the
  change evidence. An installed extension alone is no evidence.
- Exceeded agreed thresholds and identified maintainability problems MUST be fixed through
  refactoring or approved as a justified, time-limited exception.
  Exceptions require an owner, an expiry date and a linked follow-up task.
- After refactoring, the Codemetr check and the affected tests MUST be run again.
  Refactoring MUST preserve the existing behavior; domain changes are to be shown separately.
- If the tool is not available, the check MUST NOT count as passed. Release stays blocked unless
  an explicit time-limited exception has been approved.

The approved default thresholds MUST be recorded in `codemetr.json`:
type lines ≤600, constructor parameters ≤6, public methods ≤10, instance fields ≤8,
member complexity ≤25, maintainability index ≥15, coupling ≤40, inheritance depth ≤3,
member parameters ≤7 and nesting ≤4. The default prohibitions of the type name suffixes
Helper, Helpers, Util, Utils, Utilities, Manager and Processor MUST apply.
No existing debt is accepted through `codemetr.baseline.json`.
From the project root, `dotnet tool run codemetr-csharp -- check --format json` MUST be run as the
complete check; the local tool version MUST be pinned.
CI MUST run this check on Windows and Linux. Exit code 1 (findings) or 2 (input/invocation error)
MUST block release. Zero measured classes is no code release evidence. Measurement limits and
skipped code MUST be assessed.
Changes to version, rules, measurement scope or debt baseline require explicit approval;
thresholds MUST NOT be weakened merely to pass.

### IV. Test-driven development and reproducible quality

- TDD (Test-Driven Development) MUST be applied to every new or changed behavioral requirement:
  first write an automated behavioral test, then observe its justified failure (Red), then add only
  the implementation needed to satisfy it (Green) and only afterwards improve the structure
  (Refactor). Affected tests MUST stay green during refactoring.
- Before the first production behavior, the associated tests MUST be executable. Minimal
  signatures and behaviorless test/project scaffolds are permitted for executability;
  a pure compile, installation or infrastructure failure is no Red evidence for the behavior to be
  developed. Tests written afterwards do not count as TDD.
- The change evidence MUST contain the requirement or defect, the test identifier, the executed test
  command, the observed domain Red reason and the subsequent Green result.
  Tests that were not run MUST NOT be documented as Red or Green.
- Defect fixes MUST first be reproduced by a failing regression test. Changed integration contracts
  and persistence/recovery rules MUST be captured by suitable failing contract or integration tests
  before their implementation.
- Provider-independent behavioral rules MUST be verifiable deterministically without production
  access within the TDD cycle. Real provider and platform acceptance remain additional release
  evidence; they do not replace the Red evidence, and fakes MUST NOT be passed off as such acceptance.
- Pure behavior-preserving refactorings MUST have a green relevant test suite before they start and
  MUST satisfy the same assertions afterwards. No artificially red test is required as long as no
  behavior changes. Pure documentation or build-metadata changes need suitable validation instead
  of invented behavioral tests.
- New or changed domain logic MUST receive automated tests for regular flows, relevant edge cases
  and failure cases. Defect fixes require a regression test.
- Interface changes, persistence and external integrations MUST be covered by suitable integration
  or contract tests to the extent they are affected by the change.
- Tests MUST be executable independently and repeatably. Unit tests MUST NOT require production
  services, real credentials or uncontrolled network access.
- The automated tests MUST reach at least 87 % line coverage for all project-owned production code,
  not only for changed lines. Production code that is not executed MUST be included in the
  measurement. Test code does not count toward the measurement scope; generated code and
  third-party code may only be excluded with a documented justification. Exclusions merely to meet
  the threshold are prohibited.
- CI MUST measure line coverage reproducibly on Linux and Windows for the version to be released and
  MUST enforce at least 87 % per platform. Missing measurements or a shortfall MUST block release.
  Tool version, measurement command, measurement scope, exclusions and coverage report MUST be
  traceable as check evidence.
- Before release, restore, build, format/analyzer check and affected tests MUST pass with documented
  commands. Known failures are to be disclosed, not presented as passed checks.
- Restore, build and the automated tests MUST pass on both Linux and Windows before release.
  Platform-dependent functions MUST be tested on the respective target system; skipped tests are no
  evidence of behavior guaranteed across platforms.
- CI MUST run restore, build, format/analyzer check and automated tests on Linux and Windows. Every
  change intended for release MUST pass these checks for the same version; local results do not
  replace them. The complete Codemetr CLI check MUST also have passed for this version.

Metrics complement this behavioral evidence; they replace neither tests nor code reviews.

### V. Security and traceable operation

- Secrets and personal data MUST NOT be disclosed in source code, test data or logs.
  Configuration and secrets MUST be managed separately.
- Inputs at trust boundaries MUST be validated. Access MUST use the minimal necessary permissions;
  database queries MUST be parameterized.
- Dependencies MUST be checked for maintenance status, license and known security vulnerabilities
  before they are introduced. Open security findings require a documented risk assessment;
  critical findings block a release without an explicit risk approval.
- Direct and transitive NuGet dependencies MUST additionally be checked automatically for known
  security vulnerabilities before every release and at least weekly. Results MUST be traceable;
  findings require an owner and a risk-based remediation deadline. The existing rules on risk
  approval continue to apply.
- Errors MUST be handled traceably; silently swallowing exceptions is prohibited. Operationally
  relevant flows MUST provide structured, data-minimizing diagnostics.

### VI. English for all maintained project content

- All maintained project content MUST be written in English. This covers this constitution,
  source identifiers and comments, public APIs and messages (including exception and log messages),
  test names and descriptions, README files, documentation, examples and tutorials, specifications,
  plans, task lists, ADRs, checklists, contributor and agent instructions, messages emitted by
  project scripts and CI definitions, and generated source documentation. The rule is not limited
  to public or shipped files.
- Exempt as data, not as documentation: technical identifiers fixed by an external contract,
  literal test data that exercises language or Unicode handling, verbatim quotations (including
  quoted owner decisions, which are followed by an English rendering) and proper names. Translating
  them would change their meaning; the exemption does not permit non-English explanatory text.
- Immutable release artifacts, commit history and signed or hashed qualification evidence MUST NOT
  be rewritten to satisfy this rule. Such archived material stays byte-identical; where it is
  non-English, an English maintained explanation (for example an index note beside the archive)
  MUST state what it is and that it is preserved unchanged.
- Existing non-English maintained content is not grandfathered. It MUST be inventoried with a
  disposition per file, including archived material, and migrated under a concrete, owned plan whose
  completion criterion is that no maintained file without a recorded data, quotation or archive
  disposition remains non-English; new and changed content MUST be English immediately.
- Compliance is checked in review. Automated probes MAY flag candidates (for example non-ASCII
  letters or common non-English words) but MUST NOT be presented as proof that text is English, and
  no probabilistic language score is an acceptance criterion on its own.
- Conversation with the project owner or other people outside the repository may use any language;
  everything written into the repository is governed by this rule.

## Technical constraints

- The application MUST be supported on Linux and Windows. Concrete supported operating system
  versions, Linux distributions and processor architectures MUST be fixed in the technical plan
  before implementation starts.
- Application type, supported .NET/SDK version and required libraries MUST be fixed in the technical
  plan. UI technology and dependencies MUST support both target platforms; this constitution does
  not fix a concrete UI, database or deployment platform without a domain need.
- Platform-dependent APIs and native libraries MUST be isolated behind clear interfaces. For
  guaranteed functions an implementation MUST exist on both target platforms; restrictions require
  an explicit scope decision.
- File paths MUST be processed through platform-appropriate .NET APIs. Hard-coded drive letters,
  path separators and assumptions about case sensitivity are prohibited. File access and process
  invocations MUST account for differences in permissions, line endings and shell behavior where
  these are relevant for the function.
- Reproducible start and publish instructions and the required runtime prerequisites MUST be
  documented for both target platforms. Development tools need not run on both platforms as long
  as the required check evidence exists.
- The project documentation MUST cover setup, configuration, restore, build, tests and execution
  on Linux and Windows. Changes to these flows MUST update the associated documentation in the same
  change set.
- Machine-readable technical data MUST be serialized and parsed with explicitly culture-invariant
  formats. User-facing input/output MUST use a deliberately chosen culture; implicit dependencies on
  the system culture are prohibited.
- Unambiguous points in time MUST be stored and transferred in UTC or with an explicit offset;
  local presentation MUST stay separate from this. Domain calendar dates and local appointments
  MUST be modeled with suitable date/time semantics and, where needed, a named time zone. Culture-
  and time-zone-dependent behavior MUST be tested.
- Builds MUST be reproducibly executable from a clean working state with a documented SDK version
  and declared dependencies.
- Public interfaces and persisted data formats MUST be documented.
  Incompatible changes require an impact analysis and a migration plan.
- Performance requirements MUST be defined measurably where there is a relevant domain need.
  Optimizations require measurements; code quality metrics alone are no performance evidence.

## Development and release process

1. Before implementation, scope, acceptance criteria and technical planning MUST exist.
   Before every extension, the documented deep dive into the existing code including the
   architecture analysis according to principle II MUST additionally exist; it precedes the
   extension design and the test-first implementation and replaces neither TDD nor later quality
   checks. The plan MUST check compliance with this constitution and necessary exceptions.
   For every behavior-changing implementation unit it MUST name the tests to be written first, the
   expected domain Red reason and the following minimal Green implementation.
2. Changes MUST stay small and traceable. Functional changes and extensive refactorings MUST be
   reviewable separately.
3. Before release, build, analyzer, test, coverage and Codemetr results MUST exist.
   The coverage evidence MUST prove at least 87 % line coverage per target platform.
   For new or changed behavioral requirements the Red–Green evidence MUST also exist; a test suite
   that is only green at the end does not prove a test-first flow.
   The CI evidence for Linux and Windows and the current dependency check MUST cover the version to
   be released; affected documentation MUST be current.
   The Codemetr evidence MUST name the checked working state and contain the CI check.
4. Every change MUST be reviewed by the project owner or a named reviewer for behavior,
   understandability, security and constitution compliance.
   The review MUST explicitly cover SOLID contracts, business rules maintained in several places and
   anti-patterns including magic values; Codemetr alone does not replace it.
   For adapter/session changes it MUST additionally check backend neutrality, explicit capability
   boundaries and the actual representability of the guaranteed agent and target host contracts.
   Vendor-neutral method names alone are not sufficient. The review MUST also check that new and
   changed content is English according to principle VI.
   In single-person development a documented self-review is required.
5. Missing evidence and unresolved mandatory findings block release.
   Exceptions MUST be explicitly approved and documented with a time limit before release.

## Governance

This constitution is the binding project foundation. In case of contradictions, plans and
implementation MUST be adjusted or a formal amendment of this constitution MUST be approved.

Amendment proposals MUST contain the reason, the changed rules, the affected flows and, where
applicable, a migration plan. The project owner MUST explicitly approve the amendment. The version
and the amendment date MUST be updated with it.

The version follows Semantic Versioning: MAJOR for incompatible removals or redefinitions of rules,
MINOR for new principles or materially expanded requirements and PATCH for clarifications without a
change in obligations. The initial definition starts at 1.0.0.

Compliance MUST be checked during every technical planning and every change release.
Time-limited exceptions MUST be reassessed at the latest when they expire; they do not change the
constitution silently. These rules apply from their adoption; a historical adoption date is not
invented without confirmation.

### Amendment record

- 2.1.0 (2026-11-01): owner request "Make English mandatory everywhere in the Monstertacle
  constitution, including documentation" (recorded 2026-11-01T20:43:46Z). Added principle VI and
  the review obligation in process step 4; translated the constitution from German into English
  without changing any other obligation (feature spec 011, reviewed by two opposed roles for
  normative fidelity). Affected flows: every change review (step 4) and all documentation, spec and
  instruction writing. Migration plan: spec 011 `inventory.md`, which translates every maintained
  non-English file in the same feature and records the disposition of data, quotations and
  archives. The owner's request is the approval of this amendment; the exact wording is reviewed in
  the amendment pull request. Removed the Sync Impact Report comment that version 2.0.0 had left in
  the committed file although it was marked for removal before commit.
- 2.0.0 (2026-10-28): minimum line coverage gate redefined from 90 % to 87 % per native platform
  with an unchanged overall scope (owner approval "87 % code coverage sind okay, bitte anpassen"
  ("87 % code coverage is okay, please adjust"), 2026-10-28).

**Version**: 2.1.0 | **Ratified**: TODO(RATIFICATION_DATE): confirm the original adoption date | **Last Amended**: 2026-11-01
