# Current source analysis
Created: 2026-10-30T07:41:16Z
Owner: Alexander Bleissem
Multisession base: e6a9c6a plus active feature003, reviewed with two opposed architecture agents.
SharpClaw: 8ca48622050721686a41c06a63d2735182ff3544, clean main, freshly fetched and still current at this observation.

IAgentBackend, ICliProtocol, Session, RunContext and Observation are unchanged since 7854a12. AgentPluginContract.Version remains3. RunSessions.Prepare and SessionSelection.Commit split pure selection from durable custody; RunReservation rechecks admission before effects and commit. These are host policies, not new SDK rules.
CliBackend composes ICliProtocol and CliExecution. CliExecution waits for the owned process, drains streams, invokes Native.CleanupChildren and CliShutdown. Native.CleanupChildren enumerates /proc/self/task/*/children and kills every child: direct shared-process execution would have a cross-session ownership hazard. Native.BecomeSubreaper is likewise process-scoped. Inspect/cancel and external certainty are not generic promises of IAgentBackend/BackendResult.
Observation uses synchronous Action while ISessionProvider requires awaited durable observations. Native identity cannot be silently replaced; a protocol with only late native IDs does not automatically fit a preallocated-ID reference bridge.
Existing SDK public ISessionProvider/ProviderBinding/ProviderReply and CQRS paths express process ownership, immutable IDs, certainty, bounded cancellation and recovery; no missing generic type is established.

Decision: retain SDK architecture, qualify a narrowly supported out-of-process consumer reference against actual CliBackend/CLI execution, document unsupported cases. The measurement driver belongs outside repositories under ~/dev/e2e-harness-bundles; no copied SharpClaw production classes. A negative isolated process-control probe establishes the ownership hazard before claiming safety.
