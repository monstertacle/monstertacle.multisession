# External dependency ownership

Verified: 2026-10-31T15:39:46Z (UTC shell clock and subsequent Git status checks).

The owner maintains Monstertacle and SharpClaw. Microsoft Agent Framework and its Durable extension are external dependencies. Neither ownership of those upstream repositories nor an upstream API change is required by this feature.

Both reference clones remain read-only and have empty porcelain status:

- `microsoft/agent-framework`: `b7884ad88eb85c27b2a7bf40c0e15948e7ccc28d`.
- `microsoft/agent-framework-durable-extension`: `5289ab6dae5bc3e0ad1bc16889b338854be33525`.

Framework packages remain pinned to 1.27.0. Proposed invocation and outcome contracts are Monstertacle-owned integration data passed through the framework's public extension points. They do not modify or claim control of external SDK APIs. The Durable extension is reference material; no second durable scheduler is introduced.

The source changes in this increment affect only the isolated Monstertacle feature clone. SharpClaw's actual execution-boundary replacement and its live cutover remain separate acceptance steps. The concurrent owner session is untouched.
