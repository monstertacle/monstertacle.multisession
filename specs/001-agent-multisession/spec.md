# Feature Specification: Cross-Vendor Agent Multisession

**Feature Branch**: Logical feature ID `001-agent-multisession`; repository initialization
and first push to `main` explicitly approved on 2026-10-28. No automatic feature-branch requirement.

**Created**: 2026-10-26

**Updated**: 2026-10-28

**Status**: Incremental implementation; full acceptance and product release open.

**Input**: Summary of the user description: One or more reusable
NuGet libraries for parallel sessions of different AI agents and vendors.
Later hosts are to be able to accept requests permanently as a console application, Windows
service or Linux daemon. External research projects are not part of the solution.

**Clarifications** (2026-10-26): Vendor-independent usage following the functional model
of OpenCode: configurable providers and models instead of a fixed, limited agent selection.
Sessions and history as well as accepted waiting requests MUST survive a host restart
(answer to question 2: B+C). Uncertain running executions are not automatically repeated.
As a planning preference, a shared platform-neutral core and Windows/Linux libraries
depending on it are to be evaluated. The final number of packages depends on
the platform functions actually required. External reference projects serve only for
temporary ideation; temporary analysis material that is not required is removed after
its findings have been adopted. Mandatory architecture/target-host evidence under
Constitution II is exempt from this and is retained permanently in cleaned-up form.
To be distinguished from this is the new product requirement: SharpClaw is an explicitly
named long-term target consumer of the library, not a library dependency.

## Clarifications

### Session 2026-10-27

- Q: Should the library support, per agent profile, either plain model responses or an agent flow with approved tools? → A: Initially, both operating modes with automatic tool use were chosen. This provisional decision for a library-owned tool loop was superseded by the final scope clarification of this session: session functions are owned exclusively by the provider.
- Q: Should a library instance serve only one shared trust domain, or support multiple mutually isolated users or customers? → A: One trust domain per instance; the host controls access. Sessions remain separate. Internal tenant separation is not part of the delivery scope.
- Q: Should the library leave context management entirely to the respective provider and return an understandable context-limit message when support is missing? → A: Yes. The library exclusively manages multiple sessions; their functions, including context management and compaction, belong to the provider. It performs neither its own compaction nor its own tool loop and does not silently remove content. Provider- or model-related limits are reported as such.

Renewed clarification review of 2026-10-27 after the addition of FR-027 and Constitution v1.4.0:
The user requires mandatory TDD. The Red–Green–Refactor flow is therefore already
decided and requires no additional choice question. Functional scope, provider boundary,
trust domain and long-term embedding remain unchanged. The acceptance scenarios
are to be made concrete as executable tests before the respective behavior is implemented.
No new critical functional ambiguity identified; package/technical details remain in the plan.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Use independent sessions in parallel (Priority: P1)

As an integrator, I want to operate multiple agent sessions from different vendors simultaneously
so that independent requests do not block each other and their contexts remain separate.

**Why this priority**: Cross-vendor parallelism and separate session contexts
are the central benefit of the library.

**Independent Test**: Two deliberately delayed agent connections from different
vendors each process one session simultaneously. Responses remain unambiguously assigned.

**Acceptance Scenarios**:

1. **Given** two configured vendors and free capacity, **When** one request each
   arrives in two sessions, **Then** both executions begin before the first one ends.
2. **Given** two sessions with different contents, **When** follow-up requests arrive,
   **Then** each agent receives exclusively the context assigned to its session.
3. **Given** multiple requests to the same session, **When** an execution is already running,
   **Then** further requests are processed in acceptance order, without context overlap.

---

### User Story 2 - Select an agent and continue a session (Priority: P1)

As an integrator, I want to select a configured agent, open a session and
direct further requests to the same session without controlling vendor-specific flows myself.

**Why this priority**: Uniform session management makes the function reusable in other
applications and prevents lock-in to a single vendor.

**Independent Test**: An agent processes a first request and a context-dependent
follow-up request; session, status and result can be queried independently of other sessions.

**Acceptance Scenarios**:

1. **Given** a valid configured agent, **When** a session is opened,
   **Then** the integrator receives a unique session identifier and the agent assignment.
2. **Given** an existing session, **When** a follow-up request arrives,
   **Then** the session remains assigned to the same agent and its previous context.
3. **Given** an unknown agent identifier or invalid configuration, **When** a session
   is to be opened, **Then** an unambiguous error occurs without substitution by another agent.
4. **Given** a closed session, **When** a new request for it arrives,
   **Then** it is rejected; a new session requires a new identifier.
5. **Given** an additional access with an already supported vendor contract, **When** the
   integrator configures endpoint, models and credential reference, **Then** an
   agent profile can use it without changing the session management.
6. **Given** a vendor with a previously unsupported contract, **When** a suitable
   extension is plugged in, **Then** its models can be used via the same session flows
   without rewriting existing providers or the consuming application.
7. **Given** two agent profiles with different tasks, **When** both use the same
   available model, **Then** profiles, instructions and session contexts remain separate.
8. **Given** an existing provider session, **When** further requests arrive,
   **Then** the library hands them to the assigned provider session without itself
   implementing tool loops, summaries or other session functions.
9. **Given** a context limit reported by the provider, **When** a follow-up request cannot
   be processed, **Then** the error is assigned to the affected request;
   stored contents remain unchanged and are neither truncated nor summarized.
10. **Given** a provider with its own automatic context management, **When** it
    compacts the session internally, **Then** the library keeps the same session assignment
    and hands over further requests without performing an additional compaction of its own.

---

### User Story 3 - Control errors, cancellation and load (Priority: P2)

As an operator, I want to be able to observe, cancel and limit executions
so that the failure of one agent does not shut down other sessions or occupy resources without limit.

**Why this priority**: Continuous operation requires controllable errors and limited resources.

**Independent Test**: While one session fails or is cancelled, an
independent session completes its request successfully; all results remain attributable.

**Acceptance Scenarios**:

1. **Given** two active sessions, **When** one vendor fails, **Then** only the
   affected execution reports an error; the other session keeps working.
2. **Given** a running or waiting request, **When** the integrator cancels it,
   **Then** its result is marked as cancelled and not reported as successful.
3. **Given** exhausted execution capacity and a full queue, **When** a
   further request arrives, **Then** it is rejected with an unambiguous overload message.
4. **Given** active sessions, **When** a controlled shutdown begins, **Then**
   no new requests are accepted; open requests end within the configured
   period regularly or with cancellation or an explicitly uncertain execution status.

---

### User Story 4 - Embed in different long-running hosts (Priority: P2)

As an application developer, I want to obtain the function as a reusable library
and embed it in a later console process, Windows service or Linux daemon without
redeveloping the session logic for each operating mode.

**Why this priority**: The deliverables are libraries, not a hard-wired service.

**Independent Test**: A small consuming application references the packages, starts
processing and handles requests arriving at different times without restarting the library.

**Acceptance Scenarios**:

1. **Given** the delivered NuGet packages, **When** a consuming application
   references them, **Then** it can use agent sessions without copying source code or installing
   a third-party reference project.
2. **Given** started processing without current requests, **When** a new
   request arrives later, **Then** it is processed without a restart until the host requests termination.
3. **Given** a console process on Windows or Linux, **When** the same
   session flow is run through, **Then** the same functional guarantees apply.
4. **Given** a host with its own lifecycle and request intake, **When** it embeds the library,
   **Then** it can control start, request handover and termination itself; the library
   enforces neither a network port nor a service installation.
5. **Given** the shared library without a concrete platform integration, **When** an
   integrator checks the session flows with controlled test connections, **Then**
   neither Windows- nor Linux-specific runtime mechanisms are required.
6. **Given** a host with the matching platform integration, **When** the guaranteed
   session flows are executed, **Then** the same functional rules apply on both
   target systems; unsupported platform capabilities are reported explicitly.
7. **Given** two instances configured separately by the host with different
   trust domains and separate state stores, **When** sessions and providers
   are registered, **Then** one instance cannot access the sessions, results or provider
   registrations of the other instance via the library operations.
8. **Given** an existing consuming application with its own lifecycle and its own
   provider connections, **When** it embeds the NuGet library in its own process,
   **Then** it can use session opening, parallel requests, follow-up requests, status, cancellation
   and termination via public contracts without having to copy source code or start an
   additional multisession service. SharpClaw is the intended long-term
   productive target consumer of this embedding.

---

### User Story 5 - Resume sessions and waiting jobs after restart (Priority: P1)

As an operator, I want to continue using existing sessions with their history after a planned
or unexpected host restart and to work off accepted waiting requests,
without losing jobs or triggering possibly already executed work twice.

**Why this priority**: Restart resilience is explicitly part of the first delivery scope
and a prerequisite for reliable continuous operation.

**Independent Test**: Two sessions contain completed, waiting and one running
request. After a forced process termination and restart, context and assignment are preserved;
waiting requests are continued, the interrupted execution is not blindly repeated.

**Acceptance Scenarios**:

1. **Given** a session with a completed request, **When** the host restarts with the same
   stored state, **Then** identifier, profile/model assignment, history
   and result are available again; a follow-up request can use the previous context.
2. **Given** durably accepted requests that have not yet started, **When** the host
   restarts, **Then** these are worked off without resubmission under their previous identifiers
   and in the original order per session, respecting all limits.
3. **Given** a request already started before process termination with an unconfirmed outcome,
   **When** the host restarts, **Then** it is shown as interrupted with an uncertain outcome
   and is neither reported as successful nor automatically executed again.
4. **Given** such an uncertain request, **When** follow-up requests of the same session are waiting,
   **Then** they remain blocked until explicitly resolved by the integrator;
   independent sessions may keep working. A re-execution requires an
   explicit new job and a notice about a possible duplicate external effect.
5. **Given** a non-writable state store, **When** a request is submitted,
   **Then** no durable acceptance is confirmed and no external execution is started.
6. **Given** a retention rule and deletable completed sessions, **When** the
   integrator triggers their deletion, **Then** history and results are no longer retrievable via
   the library; waiting, running or unresolved jobs are not silently deleted.

---

### Edge Cases

- Missing credentials, unavailable agent programs or invalid agent configuration:
  the affected session cannot start; no silent vendor switch.
- Simultaneous requests to the same session: ordered processing instead of parallel context modification.
- Cancellation simultaneous with completion: exactly one final local result per request.
- Timeout or connection loss after handover to a vendor: report uncertain external
  execution explicitly; no automatic retry with possible side effects.
- Reject a locally known invalid configuration before handover; report functional limits
  only detectable at the provider as errors of the affected request. A complete
  feature catalog or upfront proof of all vendor capabilities is not required.
- Closing a session with open requests: reject new requests, terminate open ones in a controlled way.
- Full load: limited queue size and unambiguous rejection when the
  limit is full. For already accepted waiting requests there is no fixed guarantee
  of a maximum waiting time; the host can cancel them explicitly.
- Two writing agent jobs affect the same declared workspace:
  no simultaneous write access within the same library instance.
- Host crash between external handover and stored result: show uncertain state,
  no automatic retry or continuation of dependent requests.
- Host crash after durable acceptance but before response to the caller: request
  remains findable under its identifier; resubmission is not automatically deduplicated.
- Missing provider, changed model availability or expired credentials after restart:
  block the affected session and report the cause; no silent substitute, other sessions keep running.
- Unavailable, full or corrupted state store: no unsecured acceptance
  or unnoticed replacement by an empty state; halt the affected processing safely.
- External agent possibly still running after host crash: lock the affected workspace
  until resolution is confirmed, not only the interrupted session.
- A configurable compatible access does not support an expected capability:
  reject configuration or execution; protocol similarity is not proof of capability.
- Provider reports a context overflow or an unsupported session function:
  attribute the cause, do not start a substitute function, compaction or silent truncation of its own.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The solution MUST be consumable as one or more NuGet libraries
  without presupposing a third-party reference project or a specific service host.
  Acceptance: User Story 4.1.
- **FR-002**: The solution MUST support at least two vendors simultaneously within the same
  instance. Acceptance: User Story 1.1 and SC-001.
- **FR-003**: Agent profiles, provider accesses and models MUST be selectable separately.
  For already supported vendor contracts, additional accesses and models MUST be usable
  by configuring endpoint, model identifier and credential reference,
  without changes to session management or the consuming application. For differing
  vendor contracts, an extension MUST be possible without rewriting existing integrations.
  Full equivalence of all vendors is not guaranteed.
  Acceptance: User Story 2.5–2.7 and SC-008.
- **FR-004**: Integrators MUST be able to discover configured provider accesses and agent profiles
  and select them specifically. Invalid local configuration MUST be rejected before the
  affected execution begins. Provider-side configuration and
  function errors MUST be reported unambiguously; a catalog or replica of its own of
  all session functions is not required. Acceptance: User Story 2.3 and 2.9.
- **FR-005**: Integrators MUST be able to create sessions, query their state and
  close them. Each session MUST have a unique identifier and a fixed agent assignment.
  Profile, provider, model and context-relevant settings MUST be recorded at creation;
  later configuration changes MUST NOT switch running or restored
  sessions unnoticed. Acceptance: User Story 2.1–2.4 and 5.1.
  An uncertain session opening MUST remain findable and explicitly resolvable even without an
  existing request; a new native session MUST NOT be created silently.
- **FR-006**: Each request and its result MUST be unambiguously assigned to a session and a
  request identifier. Session histories and credentials MUST NOT be
  inadvertently shared between sessions or vendors. Acceptance: User Story 1.2.
- **FR-007**: Follow-up requests MUST be able to use the previous context of their session.
  Requests of the same session MUST be executed in acceptance order.
  Acceptance: User Story 1.3 and 2.2.
- **FR-008**: Independent sessions MUST work in parallel up to the configured capacity.
  Simultaneous executions and waiting requests MUST be configurably limited;
  full queues MUST reject further requests. Acceptance: User Story 1.1 and 3.3.
  FIFO applies per session. Among admissible session heads, the oldest acceptance has
  priority; blocked heads MUST NOT lock independent admissible heads.
  A fixed waiting-time or general starvation-freedom guarantee is not given.
- **FR-009**: The integrator MUST be able to distinguish per request at least waiting, running, successful,
  failed, cancelled and timed out. In case of uncertain
  external execution, this uncertainty MUST additionally be recognizable. Final states MUST
  be unambiguous. Acceptance: User Story 3 and cancellation/connection-loss edge cases.
  The canonical states are Queued, Running, Succeeded, Failed, Cancelled, TimedOut
  and Interrupted. Interrupted denotes a job found started but unresolved at restart.
  Execution certainty, session lifecycle and blocking are separate
  attributes; a local final state alone does not prove that external work has ended.
- **FR-010**: Integrators MUST be able to cancel individual waiting and running requests and
  set timeouts. Local processing MUST end; if an external
  execution cannot be confirmed as terminated, this MUST be reported. Acceptance: User Story 3.2.
- **FR-011**: Errors of one session MUST NOT terminate independent sessions.
  Credential errors, overload, timeout and execution errors MUST be distinguishable.
  Acceptance: User Story 3.1 and the error edge cases.
  Continued work presupposes free remaining execution capacity. If all slots
  are reserved by running or uncertain work, further requests remain waiting;
  limits or reservations MUST NOT be bypassed for the sake of apparent error isolation.
- **FR-012**: There MUST be neither a silent vendor switch nor an automatic
  retry of a request that may already have been executed.
  Acceptance: User Story 2.3 and the connection-loss edge case.
- **FR-013**: The libraries MUST remain available for further requests until termination is requested
  and allow a controlled shutdown with a finite, configurable
  period. Acceptance: User Story 3.4 and 4.2.
  A controlled stop explicitly ends remaining waiting requests as
  Cancelled/NotStarted; running requests end confirmed or with reported
  uncertainty. Queued requests not conclusively ended at a crash remain
  resumable. This rule deliberately distinguishes stop from process crash.
- **FR-014**: The libraries MUST fulfill the same functional contract on Linux and Windows
  and be embeddable independently of console, Windows service or Linux daemon.
  Service installation and request transport belong to the later host. Acceptance: User Story 4.3–4.4.
- **FR-015**: Status and error diagnostics MUST allow request and session to be attributed, but
  MUST NOT disclose credentials or, by default, complete confidential request
  contents. Acceptance: diagnostics check of the scenarios from User Story 3.
- **FR-016**: For agent jobs with write access, a declared shared
  workspace within the same instance MUST be protected against simultaneous writing executions;
  independent workspaces may continue to work in parallel.
  Acceptance: workspace edge case with an independent third job.
- **FR-017**: The delivery MUST contain traceable instructions for package acquisition,
  agent configuration, first session, follow-up request, cancellation and termination on both
  target platforms. Acceptance: SC-006 and User Story 4.
- **FR-018**: Session identifiers, used profiles/providers/models, context required for follow-up
  requests, results and request states MUST survive host restarts.
  Accepted, not yet started requests MUST be processed further after restart under the same identifiers
  and in the same order per session. Limits and locks
  also apply during recovery. Acceptance: User Story 5.1–5.2 and SC-009.
- **FR-019**: A request MUST NOT be confirmed as accepted or started externally until its input,
  identifier and session assignment have been durably saved. Storage failures
  MUST be visible; missing or corrupted state MUST NOT be silently replaced by an
  empty state. Acceptance: User Story 5.5 and storage edge cases.
  Public errors MUST distinguish proven non-acceptance from unknown commit outcome.
  A local candidate identifier generated before commit serves for lookup,
  not as a receipt or deduplication promise; a confirmed commit remains accepted.
- **FR-020**: After restart, already started requests without a saved final state MUST be
  recognizable as interrupted with an uncertain outcome. Without explicit resolution, there MUST
  be neither automatic retry nor further executions in the affected session
  or a possibly still occupied workspace. Independent sessions
  MUST remain usable. Acceptance: User Story 5.3–5.4 and the external-agent edge case.
  A close request does not remove an uncertain reservation: the session remains
  Closing and blocked until termination is resolved. Only then is Closed permissible.
  Resolution cases MUST have a publicly readable current revision; stale or
  competing decisions MUST NOT cause partial resolution or release.
- **FR-021**: Integrators MUST be able to define retention rules for stored contents
  and delete completed sessions including their contents. Waiting,
  running or unresolved jobs MUST NOT be silently removed in the process. Credentials
  MUST NOT become part of stored histories; the host MUST be able to protect the storage against
  unauthorized access. Acceptance: User Story 5.6 and security review.
  Empty or oversized inputs are to be rejected before acceptance. An output above the
  configured limit results in a reported error instead of a silently truncated
  success. Already stored contents remain unchanged; if the external end
  cannot be confirmed, the rules for uncertain execution apply.
- **FR-022**: The delivery MUST demonstrate at least two different vendor contracts and
  configurable compatible accesses. A single vendor or a
  mandatory upstream OpenCode process MUST NOT be a prerequisite for session usage.
  Supported contracts, capabilities and limits MUST be documented. Concrete
  reference vendors are selected in the technical plan as evidence, not as a fixed
  limitation of the product scope. Acceptance: User Story 1.1, 2.5–2.6 and SC-008.
- **FR-023**: Shared session contracts and domain logic MUST be usable and testable without a concrete Windows or
  Linux implementation. Operating-system-specific mechanisms
  MUST be integrated separately from them and fulfill the same functional contract;
  missing capabilities MUST NOT silently count as supported.
  Acceptance: User Story 4.5–4.6 and SC-007.
- **FR-024**: The library MUST hand requests and follow-up requests to the assigned
  provider session. Tool execution and internal agent loops MUST
  remain with the provider; they MUST NOT be implemented or replicated as a library function of its own.
  The context/content rules are stated bindingly in FR-025.
  Acceptance: User Story 2.8 and 2.10 and SC-010.
- **FR-025**: The library MUST NOT itself summarize context or silently truncate it.
  If a provider supports its own context management, its use MUST be possible without additional
  library-owned compaction. Provider/model limits MUST be reported as errors
  of the affected request without modifying stored histories.
  Acceptance: User Story 2.9–2.10 and SC-010.
- **FR-026**: A library instance MUST represent exactly one shared trust domain.
  Authentication and access authorization for callers belong to the host;
  internal user/tenant management is not included. Separate instances
  MUST NOT mix session data, provider registrations and state through global mutable
  library state. Acceptance: User Story 4.7 and the context separation from SC-002.
- **FR-027**: The library MUST be usable in the long term as an embedded component of SharpClaw.
  Public library contracts MUST enable its host-side integration of
  lifecycle, configuration and its own provider adapters without introducing a
  dependency of the library on SharpClaw, source code copies or a mandatory
  separate multisession process. The actual migration of the application
  is a separate later integration project. Acceptance of embeddability:
  User Story 4.8 and SC-011.

### Key Entities *(include if feature involves data)*

- **Vendor**: Provider of an agent product; determines available accesses and prerequisites.
- **Provider access**: Configured access to a vendor or compatible service with
  endpoint, supported contract, credential reference and available models.
- **Model**: Model available via a provider with identifier, capabilities and limits.
- **Agent profile**: Named task with instructions, selection of a provider and model
  and execution limits. Multiple profiles may use the same model.
- **Session**: Identifiable, isolated conversation and execution context with a fixed agent,
  lifecycle and ordered requests.
- **Request**: Job within a session with identifier, input, acceptance order,
  optional workspace, timeout and processing state.
- **Result**: Assigned outcome of a request with output or error, final state
  and, where applicable, a notice of uncertain external execution.
- **Workspace**: Working context declared by the integrator whose writing use
  MUST NOT take place simultaneously through multiple jobs of the same instance.
- **Recoverable state**: Saved sessions, contexts, results and waiting
  jobs including order and locks; basis for resumption and state resolution.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a controlled acceptance run, at least two vendors process
  a total of ten independent sessions with two requests each. With sufficient capacity,
  at least two requests from different vendors demonstrably run overlapping in time.
  The two requests per session are submitted in order; at least two
  independent first requests are kept open in a controlled way until the overlap is detected.
- **SC-002**: With 100 mixed incoming requests across ten sessions, 100 % of the
  results are assigned to the correct request and session; no foreign session contents
  get into another session, and each session maintains its acceptance order.
  The controlled run comprises ten requests per session with unique
  request/session markers; the target order is the confirmed acceptance sequence,
  not the start order of clients calling in parallel.
- **SC-003**: With five deliberately faulty and five independent successful
  requests, all ten final states are reported correctly; all five independent
  requests complete successfully despite the foreign errors.
  The five errors are confirmed terminated executions; uncertain occupied slots are
  considered separately and MUST NOT artificially relieve this error isolation test.
- **SC-004**: In the controlled test with cooperative providers, available local
  storage and requests kept open in a controlled way until cancellation, a durably queryable
  Cancelled/TimedOut final state including correct execution certainty is visible at the latest
  two seconds after receipt of a cancellation request or
  after expiry of the request deadline. Time is measured monotonically;
  receipt at the manager or the actual deadline end is the start of measurement.
  With ShutdownTimeout=ten seconds, the local controlled stop ends at the latest
  ten seconds after receipt of the stop request. Still unconfirmed external work
  remains Unknown with reservation; no open request disappears without a state.
  This does not promise forced termination of a non-cooperative provider nor
  successful storage when the storage medium has failed.
- **SC-005**: With an execution limit of two and a queue limit of three,
  two requests held in a controlled way in separate sessions run first. Then
  eight further requests arrive simultaneously: exactly three are accepted as waiting and
  five rejected because of a full queue. Thus a total of five of ten requests are
  accepted, neither of the two limits is exceeded and no running request
  ends before all acceptance decisions are complete. The explicit initial state prevents
  the result from depending on the timing of the first scheduler run.
- **SC-006**: An integrator without knowledge of the library source code can, using the
  documentation, successfully execute a first request and a follow-up request within 30 minutes
  after the prerequisites have been provided.
- **SC-007**: All acceptance scenarios guaranteed across platforms pass on
  Windows and Linux. A one-hour test operation with at least one request per minute
  per target platform requires no restart and loses no accepted request.
  The hour is measured monotonically from successful start; at least 60 accepted
  requests per target system and their final states must be queryable at the end or after
  a controlled stop under their original identifier. A still
  running or uncertain request does not count as lost if state and reservation
  are saved. Unjustifiably missing records or false success states are a loss.
- **SC-008**: Two different vendor contracts are used successfully. An
  additional compatible access including model can be used solely through configuration for a first
  request and follow-up request. An extension for a third, differing
  test contract passes the same session tests without changes to session management
  or the consuming application.
- **SC-009**: With ten forced restarts, each with two sessions, two already
  completed, three waiting and one running request, 100 % of the confirmed
  requests and completed results remain findable. All three waiting requests of a
  non-locked session are processed further in order; the running request
  is reported as uncertain and is not automatically restarted in any run.
  Per run, one completed result is in session A and one in session B;
  all three Queued requests are in A, the only Running request is in B.
  A and B have separate workspaces, the execution limit is two. After
  restart, A can use the remaining free slot; B remains blocked until resolution.
- **SC-010**: With two controlled provider sessions with two follow-up requests each,
  the external session assignment remains unchanged in 100 % of handovers. A provider-side
  managed compaction triggers zero additional summaries by the library.
  A simulated context overflow is assigned to the correct request and
  modifies zero already stored history entries. There are zero library-owned
  tool executions or substitute agent loops.
- **SC-011**: An isolated consuming integration proof opens two parallel sessions via the
  public library contracts, sends one first request and
  one follow-up request each, reads their status, cancels a deliberately delayed request
  and terminates processing. Zero changes to the library source code and
  zero additional multisession processes are required. The proof checks the prerequisite
  for the later SharpClaw integration, not its already completed product migration.

The controlled acceptance runs check orchestration and lifecycle with deterministic
test agents; response quality and fluctuating vendor runtimes are not a performance guarantee.
For at least two reference vendors selected in the technical plan, a real
integration proof for session opening, first request and follow-up request
on both target platforms is additionally required. Extensibility is demonstrated separately.

## Assumptions

- Primary users are integrators and operators. End users later get access via a
  separate host; a user interface of its own is not part of this feature.
- All callers of an instance belong to one shared trust domain. The host
  MUST reject unauthorized callers before access to the library. For different
  trust domains it is responsible for separate instances, state stores and appropriate
  access rights. Instance separation within a process is not a security sandbox
  against malicious host or extension code; such isolation is not guaranteed.
- The delivery scope is libraries, package artifacts and integration documentation. A small
  consuming acceptance proof is planned, but no productive service, installer,
  network access, authentication service or transport protocol.
- SharpClaw is to consume the library itself in the long term as part of its application.
  This requirement concerns the standalone embeddability and extensibility,
  not the adoption of its architecture or functions. The productive integration,
  a possible replacement of existing management and the adoption of existing state
  require their own later scope; backward compatibility is not guaranteed across the board.
- „Permanent auf Anfragen lauschen“ ("permanently listen for requests") means here: the library
  remains usable for new jobs within the running host. The later host is responsible for their intake.
- Agents initially work independently. Automatic delegation, agent-to-agent communication,
  dynamic creation of further agents and joint decision-making are not included.
- Message content and context remain separate per session. An agent switch within
  the same session and the migration of native vendor sessions are not included.
- The integrator provides valid accesses, required runtime prerequisites and
  network connections. Vendor independence means uniform
  functional access with configurable and extensible connections, not automatic
  support of all unknown vendor protocols or sign-in procedures.
- The first delivery goal is cross-vendor management of provider sessions,
  not an agent runtime of its own. In this project, a provider is the connected
  implementation that processes the requests of a session and is responsible for its functions;
  it is not necessarily equivalent to a plain model endpoint.
  Additional remote control of arbitrary installed agent programs, the adoption
  of all OpenCode providers and a complete OpenCode-compatible agent runtime
  are not automatically part of this feature. The concrete connection technique is
  reserved for the technical plan; OpenCode is not presupposed as a mandatory runtime.
- Tool provisioning and execution remain, where applicable, with the host and its
  provider connection. The multisession management does not activate or implement a tool
  runtime of its own and does not create any general shell, file system or network access.
- Integrators declare shared workspaces consistently. A lock across
  multiple host processes and a security sandbox for untrusted agent code are not included.
- A repeated call counts as a new request. Durable deduplication, guaranteed
  exactly-once execution at vendors and automatic retry are not guaranteed.
- Restart resilience covers the same logical host with a state store that remains available,
  not high availability, parallel active hosts or recovery after loss of the storage medium.
  Native external processes are not presupposed to be automatically reconnectable.
- Retention periods are configured explicitly by the integrator; during
  retention, recoverable contents remain available. The library cannot guarantee deletion of external
  vendor data or host-side backups, and this MUST
  be documented as a boundary of responsibility.
- Domain session identity, external provider session identifier and stored context
  are different responsibilities. An external identifier alone is not
  proof of a completely recoverable session history.
- Durable acceptance, reservation, external start and saved completion are
  different state transitions. Workspace and capacity reservations MUST NOT
  be released at a restart solely because the host process has disappeared.
- Diagnostic logs are not the authoritative session or request state. A terminated
  process or a successful transport connection alone does not prove agent success.
- A shared library with dependent Windows/Linux libraries is the preferred
  split to be evaluated. Shared contracts and domain logic MUST NOT have a back-dependency
  on concrete platform libraries. Neither empty platform packages nor
  duplicated session logic solely to satisfy a naming scheme are presupposed.
- Provider connections and persistence are not treated across the board as operating-system-specific.
  Transport, UI, service installation and local secret management do not
  automatically belong in the shared library. The plan defines their concrete boundaries.
- The later remote client consumes a deliberately released host contract; it does not thereby
  automatically receive provider credentials or arbitrary control over agent processes.
  A connection profile is to be distinguished from a domain agent profile.
- Temporarily examined third-party projects create no runtime or source code dependency.
  Their specific implementations, protocols and feature sets are not
  automatically adopted. Completed temporary ideation is to be distinguished from permanently
  required architecture/target-host evidence under Constitution II.
  This evidence is retained under evidence/architecture with source revision, relative file/
  symbol references and findings, without adopting third-party product code or private runtime data.
  SharpClaw remains the explicitly named target consumer. Temporary research storage that is not
  required establishes neither a dependency nor a retention obligation.
- Constitution v2.0.0 applies, including the generic agent/provider boundary, at least 87 % line coverage and
  the mandatory official Codemetr CLI guard. TDD with observed Red–Green–Refactor is mandatory for new or
  changed behavior requirements; existing refactorings require a
  previously green relevant suite. This development rule is not an additional
  session feature. C#/.NET and NuGet are given project frameworks;
  package split, concrete SDK/operating system versions and internal architecture belong
  in the technical planning. The ratification date remains open. Approved are
  Codemetr.CSharp.Tool 0.1.0 and the default thresholds; there is no debt baseline.
- The first delivery format supports only its own explicitly versioned
  storage/binding contract. Unknown or incompatible formats are to be rejected without modification.
  Automatic migrations, format downgrades and restoration from
  backups are not part of this first delivery; a later format change requires
  its own migration and rollback plan as required by the constitution.
- OpenCode V2 was reviewed as a functional reference on the basis of the official documentation of 2026-10-26:
  [Providers](https://opencode.ai/v2/docs/providers/),
  [Models](https://opencode.ai/v2/docs/models/) and
  [Agents](https://opencode.ai/v2/docs/agents/). Relevant are separate agent profiles,
  providers and models as well as configurable compatible accesses and provider extensions.
  No repository was cloned and no source code deep dive was performed; this was
  not necessary to clarify the functional scope. OpenCode's technical implementation and
  retry rules are not thereby adopted as a project decision.
