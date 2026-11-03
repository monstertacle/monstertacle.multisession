# T009: the invocation context must not reach chat options or the wire

Base: `main` at `4b533c9`. Native Linux, memory-capped runs.

## Red (observed defect)

`tests/Monstertacle.Multisession.UnitTests/FrameworkChatClientIsolationTests.cs` drives a real `ChatClientAgent` (Microsoft Agent
Framework 1.27.0) over a recording `IChatClient` through `FrameworkSessionProvider`.

- Against the unchanged source, `InvocationContextNeverReachesChatOptionsOrTheWire` failed with "invocation key in chat
  options". The agent copies `AgentRunOptions.AdditionalProperties` into `ChatOptions.AdditionalProperties`, so the
  `FrameworkInvocation` reached the chat client. That object exposes the SDK session, request and dispatch identities as public
  properties.
- A measurement variant, with the options assertions removed for one run and the test then restored, showed that the request
  serialized with `AIJsonUtilities.DefaultOptions` contains the key `Monstertacle.Multisession.AgentFramework.FrameworkInvocation`.

## Green (smallest change)

- New internal `FrameworkRunOptions : AgentRunOptions`. It carries the invocation as a typed member and overrides `Clone` to keep
  it.
- `FrameworkInvocation.CreateOptions` returns it, and `FrameworkInvocation.TryGet` reads it from there.
- The public members and signatures of `FrameworkInvocation` are unchanged. An invocation placed in `AdditionalProperties` is no
  longer recognised; the SDK itself never does that.
- Result: 50 of 50 framework unit tests pass. They include `OnlyTheAdapterRunOptionsAndTheirClonesCarryTheInvocation`: a clone
  yields the same invocation, and an invocation smuggled in through `AdditionalProperties` is refused.

## Mutations (each observed red, then restored)

- Remove the `Clone` override: the clone test fails.
- `TryGet` falls back to `AdditionalProperties`: the smuggling assertion fails.
- The original transport (`AdditionalProperties`) is the observed Red above.

## Consumer impact

SharpClaw (`main` at `511a24a`, read only) obtains the invocation only through `FrameworkInvocation.TryGet(options)`:
`ProcessSessionAgent.cs:30`, `StructuredReviewAgent.cs:26` and `tests/MultisessionNuGet/FixtureAgent.cs:22`. Those agents
receive the adapter's options object unchanged, so they keep working without source changes. The fix changes AgentFramework
package bytes, so it needs a new prerelease and a host package update. It is proposed as 0.2.0-alpha.9.
