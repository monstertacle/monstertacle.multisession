# Feature Specification: Checkpoint base contracts and the delivery checkpoint package

**Feature Branch**: `feature/013-checkpoint-split`

**Created**: 2026-11-02

**Status**: Specified and clarified. The contract decision of 2026-11-02 is recorded under Clarifications.

**Input**: Owner commission of 2026-11-02: "Implement two assemblies: `Monstertacle.Multisession.Checkpoints` and
`Monstertacle.Multisession.Checkpoints.Delivery`, base interfaces and concrete implementation under SOLID." The owner fixed
both names and authorised the naming and API change, a new immutable package set and later host consumption.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Depend on a storage-independent checkpoint contract (Priority: P1)

A library or host author references only the base checkpoint package. They write code against its contract and provide
their own implementation, for example an in-memory test double, without loading the delivery package, SQLite or any session
package.

**Why this priority**: Without a real, dependency-free contract the base package would be an empty marker. That would not
meet the owner's request.

**Independent Test**: An isolated consumer restores only the packed base package, implements the contract with its own type,
builds, runs, and passes its own cases. Its lock file lists no delivery, SQLite or session package.

**Acceptance Scenarios**:

1. **Given** a consumer that references only the base package, **When** it restores, builds and runs, **Then** it succeeds,
   and the resolved dependency set contains no delivery, SQLite or session package.
2. **Given** the base package gains a reference to any of those packages, **When** the dependency checks run, **Then** they fail.

---

### User Story 2 - Keep durable delivery custody unchanged (Priority: P1)

A host that today keeps one delivery channel in the delivery checkpoint moves to the new delivery package. It keeps exactly
the same durable behaviour:
- the exclusive lease;
- the stored bytes and digest;
- compare-and-swap transitions;
- Sending becoming Uncertain after a crash;
- the single resend allowance;
- attempt grants and route moves;
- typed refusals;
- cancellation leaving state unchanged.

A checkpoint file written by the current package opens unchanged.

**Why this priority**: The only production consumer holds live delivery custody. A namespace split must not change what is
stored or what a refusal means.

**Independent Test**: All existing delivery checkpoint tests run unchanged in meaning against the new delivery assembly. This
includes the fixtures captured from the current host checkpoints.

**Acceptance Scenarios**:

1. **Given** a checkpoint written by the current package, **When** the new delivery package opens it, **Then** its position,
   pending bytes, phase, attempts, acknowledgement and every stored receipt are returned unchanged.
2. **Given** any transition or grant, **When** it runs against the new package, **Then** the stored table text, values and
   refusal reasons equal those of the current package.

---

### User Story 3 - Use the delivery operations through the contract (Priority: P2)

Ordinary transition code in a host depends on the smallest delivery contract it needs, not on the concrete store. Only
composition and the grant helpers use the concrete type.

**Why this priority**: The split is only useful if the real implementation implements the contract and the ordinary caller
uses it.

**Independent Test**: A shared contract test suite runs both against the real delivery checkpoint and against a test double.
A mutation that bypasses the contract in the real implementation is detected.

**Acceptance Scenarios**:

1. **Given** the shared contract cases, **When** they run against the real checkpoint, **Then** every case passes.
2. **Given** the real checkpoint no longer implements the contract, **When** the tests compile or run, **Then** they fail.

---

### User Story 4 - Release both packages as one qualified set (Priority: P2)

A maintainer releases the new package set. Both new packages are built, measured, tested on Linux and Windows, checked for
layout and dependencies, and exercised by isolated consumers. A missing package or test suite is detected rather than
silently skipped. The released 0.2.0-alpha.7 packages stay as they are.

**Why this priority**: One old package becomes two, so the release set changes. Silent omission would publish an
unqualified package.

**Independent Test**: The quality, layout and consumer checks fail when a new package or its test suite is removed from
their inventories.

**Acceptance Scenarios**:

1. **Given** a solution test project missing from the quality suite list, **When** quality runs, **Then** it fails and names
   the project.
2. **Given** the package directory lacks one of the expected packages, **When** the layout check runs, **Then** it fails.

### Edge Cases

- A consumer still compiles against the old namespace: it fails to compile. There is no alias or type forwarder.
- A checkpoint is open in the old package and a new-package process tries to open it: the shared lease refuses the second
  owner. The lease file name is unchanged.
- Cancellation before a transition commits: state stays unchanged in the new package as before.
- A grant helper is constructed from a contract instance that is not the real store: this is impossible at compile time.
  Grant helpers keep the concrete type, because their transaction needs the private connection.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST provide a package and assembly `Monstertacle.Multisession.Checkpoints`. Its public
  contracts MUST be usable without any delivery, SQLite or session dependency.
- **FR-002**: The base package MUST contain at least one non-trivial contract that the delivery implementation actually
  implements. It MUST NOT be a marker, a forwarding facade, or a store for delivery phases, transitions, grants or refusal
  reasons.
- **FR-003**: The repository MUST provide a package and assembly `Monstertacle.Multisession.Checkpoints.Delivery` that
  depends on the base package. It MUST hold all delivery-specific types: channel identity, options, phase, state,
  expectation, pending item, refusals, grants, the concrete checkpoint and its storage.
- **FR-004**: Dependency direction MUST be Delivery → Checkpoints only. The base MUST NOT reference Delivery, SQLite, the
  session packages, Microsoft Agent Framework or any host.
- **FR-005**: The concrete delivery checkpoint MUST implement the selected contract or contracts. Ordinary transition use
  MUST be possible through a contract. Grant helpers MAY keep the concrete type, because they share its private transaction.
- **FR-006**: Namespaces MUST follow the new assembly names. There MUST be no alias package, type forwarder or old-namespace
  facade.
- **FR-007**: The persisted format MUST be unchanged:
  - table definitions and their stored text;
  - column names;
  - phase strings;
  - schema value 1;
  - WAL and synchronous mode;
  - the `<state>.lock` lease;
  - payload, acknowledgement and receipt bytes.

  Files of the current package MUST open and behave identically.
- **FR-008**: Behaviour MUST be unchanged:
  - lease exclusivity and private access;
  - compare-and-swap transitions;
  - attempt counting and its overflow bound;
  - Sending → Uncertain on owner open;
  - single resend;
  - attempt grant ordering;
  - route moves;
  - idempotency and capacity;
  - typed refusal reasons;
  - cancellation.

  The libraries MUST still perform no external effect, retry or background work.
- **FR-009**: Existing delivery checkpoint tests MUST move with their subject into the delivery test assembly with unchanged
  assertions. New base-contract tests MUST NOT claim to prove delivery behaviour.
- **FR-010**: A base-only isolated package consumer MUST restore exactly the base package, implement the contract itself and
  pass. A dependency violation MUST fail it.
- **FR-011**: The delivery isolated package consumer MUST use the new delivery package and its namespaces, and pass its
  existing conformance cases.
- **FR-012**: Quality-suite discovery, product coverage scope, Codemetr inventory, package layout and canonical consumer
  checks MUST include both new assemblies. The quality run MUST fail when a solution test project is absent from its suite
  list.
- **FR-013**: Native Linux and Windows quality MUST pass with the existing 87 % product minimum and unchanged tool pins,
  thresholds and baselines.
- **FR-014**: The released 0.2.0-alpha.7 packages and their release record MUST NOT change. The new set MUST use a new,
  unused version decided before publication.
- **FR-015**: The staged pinning tests from the earlier continuity work MUST be preserved in this branch.
- **FR-016**: Documentation MUST describe both packages, the dependency direction and the host migration (package
  reference, namespaces, lock files). A superseding decision record MUST replace the earlier package-name decision.

### Key Entities

- **Checkpoint contract (base)**: `ICheckpoint<TSnapshot>`. A storage-independent, strongly typed read and lifetime
  contract over a caller-defined snapshot type.
- **Delivery checkpoint port (Delivery)**: delivery-specific transitions over the delivery state, extending the base contract.
- **Delivery checkpoint (Delivery)**: the existing concrete durable implementation over SQLite under an exclusive lease.
- **Grants (Delivery)**: attempt grants, resend authorisations and route moves, recorded atomically against an exact
  expectation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An isolated consumer of only the base package builds and passes with zero delivery, SQLite or session packages
  in its resolved dependency set.
- **SC-002**: 100 % of the existing delivery checkpoint tests pass against the new delivery assembly with unchanged
  assertions, including the captured fixtures of current checkpoints.
- **SC-003**: The shared delivery contract cases pass against both the real implementation and a test double. Each named
  mutation (reverse dependency, bypassed contract, missing suite, missing package) turns a check red.
- **SC-004**: Native Linux and Windows quality pass at no less than 87 % product line coverage with both new assemblies
  inside the measured scope.
- **SC-005**: The stored table text and row values written by the new package are byte-identical to those of the current
  package for the same operations.

## Assumptions

- SharpClaw's listener is the only production consumer. Its migration is a later, separately granted host step and is not
  part of this repository change.
- The nuget.org trusted publishing policy pattern `Monstertacle.Multisession.*` covers the two new package IDs. This is
  documented in `docs/publishing.md` but cannot be observed from the repository.
- All library packages share one repository version (`Directory.Build.props`), so the new set re-versions every package.

## Clarifications

### Session 2026-11-02

- Q: Does the base hold the delivery phases, transitions or grant refusals, so the host diff stays small? → A: No. Root
  rejected that shared premise of the first research round (r1 disposition). Phases, transitions, grants and refusals stay
  in Delivery.
- Q: Old namespace inside the new assembly, or a type forwarder? → A: No. The new namespaces follow the new names (owner
  commission; architecture delta v2).
- Q: Does the split change the persisted format? → A: No change is needed. Persisted strings are independent of C#
  namespaces (see research.md). A required change would return to root first.
- Q: What exactly is the base contract: a generic typed read/lifetime contract only, plus an opener, and does the delivery
  port extend it? → **Decided** (contract direction recorded 2026-11-02T20:51:58Z, after two opposed reviews):
  - The base is `ICheckpoint<TSnapshot> : IDisposable` with `ReadAsync` only.
  - `IDeliveryCheckpoint : ICheckpoint<CheckpointState>` adds the five existing transitions unchanged.
  - The concrete static opens stay. There is no generic opener or factory interface.
  - Grants keep the concrete store; ordinary use takes the port as a view of the same object, with one disposal owner.

  Dissent recorded and rejected:
  - the claim that an unconstrained snapshot is untyped (the closed `ICheckpoint<CheckpointState>` is compile-time typed);
  - an `IStoredBytes` projection (no independent caller, and it adds a second representation).

  Accepted risk: the generic interface could be decorative. The acceptance therefore requires a base-only typed consumer, the
  real implementation, the inherited delivery read and a host-shaped loop that fails on bypass. See
  contracts/checkpoint-contracts.md.
- Q: Which version does the new set use? → Proposed `0.2.0-alpha.8`: unused on nuget.org for every SDK package and for both
  new IDs on 2026-11-02. Root confirms it before any package is published.
