# Agent Framework migration (0.2.0-alpha.2)

Use `Monstertacle.Multisession.AgentFramework` with Microsoft Agent Framework1.27.0. The host supplies an existing AIAgent factory; endpoint/model/credential/tool/workspace configuration belongs to that agent. `SessionRuntime`, typed commands/queries, SQLite ownership and recovery remain authoritative. A factory must provide a stable ID and compatible serializer for the chosen adapter contract. Agent instances are host-owned; each call restores an independent AgentSession. Serialized session state is sensitive data stored under the same private-store boundary; never serialize credentials or log continuation JSON.

```csharp
var provider = new FrameworkSessionProvider(
    new FrameworkAgentOptions { AdapterId = "my-agent-session-v1" },
    context => hostAgentFactory(context));
var registration = new ProviderRegistration("agent", "my-agent-session-v1", 1, provider);
```

The generic adapter supports an explicitly terminal Stop response with final assistant text. Missing finish reason, background continuation, approvals and unsupported final content remain unsupported/unknown. Completed intermediate tool messages are excluded from final text. Local cancellation does not prove remote termination. Generic remote inspection/cancellation and automatic recovery are deliberately unavailable; retain custody and resolve unknown effects through the host's explicit recovery process.

## Evidence renewal

Construct an immutable EvidenceGraph from project-defined Source, Rule, Binding, Verdict, RuleDecision and Artifact nodes. Revision identifies the exact immutable required evidence/validation version; changing the artifact or judgment requires a new revision. A validator checks that a receipt actually meets that definition. Transitive fingerprints alone do not approve a judgment.

Admit work through existing durable session APIs and durably associate EvidenceWorkItem with its RequestId/SessionId and graph fingerprint before exposing it to renewal. `EvidenceWorkflow.RunAsync` only reads those requests. Missing or ambiguous association returns blocked coordination; it never submits replacement work. Recover lost admission first. Supply a pure, nonblocking project validator; it performs no external execution. Persist accepted receipts before the next round. Recreating the workflow with the same references only re-reads prior results, including when a process died after an effect but before report persistence. Unknown requests halt the round and retain custody. Attempt/deadline budgets bound observations; log or status changes cannot increase acceptance.

## Package and type changes

| Previous package/type prefix | New protocol-based package/type prefix |
| --- | --- |
| `Providers.OpenAI` / `OpenAi*` | `Providers.Conversations` / `Conversation*` |
| `Providers.Gemini` / `Gemini*` | `Providers.Interactions` / `Interaction*` |

These optional HTTP implementations describe their actual protocol contracts. Prefer the generic framework package for new host composition. Technical adapter IDs and native binding formats are unchanged so historical data is not rewritten; a namespace rename does not convert a native session into a framework-managed session. No compatibility alias classes or automatic active-session migration are provided. Keep original bindings with their qualified original protocol implementation until closed. New framework sessions use an explicit different adapter registration.

NuGet0.2.0-alpha.1 is not overwritten; new artifacts use0.2.0-alpha.2. A production SharpClaw switch still requires a real host registration and its own lifecycle/update qualification. Library tests and fixtures are not a deployed agent backend.

Optional HTTP protocol options now require an explicit Endpoint; there is no vendor endpoint default. Supply the approved compatible service URI at the host composition boundary. Existing technical authentication-header and wire-ID conventions describe protocol interoperability, not runtime provider selection.
