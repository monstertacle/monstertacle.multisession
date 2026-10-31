# Public contract changes

Canonical ISessions creation takes caller SessionId; IRequests submission takes caller RequestId separately from SubmitRequest payload. CQRS commands carry the same IDs and facades forward them unchanged. Empty and duplicate IDs reject before effects; no auto replacement. Caller recovery queries exact IDs and compares immutable context/submission fingerprint.

SessionRuntime exposes a typed targeted-dispatch command in HostControlled mode. Automatic scheduling and explicit target dispatch share the same atomic store policy; incorrect mode/target cannot fall back to another queued request. Mode persists in format2 and is checked before recovery/start. Controlled shutdown preserves queued input.

The framework package adds its own qualified typed outcome and invocation-context contracts using public AIAgent/AgentSession/AgentRunOptions APIs. Microsoft Agent Framework remains an unmodified external NuGet dependency. No internal framework access, fork or upstream patch is required.

Typed outcome admission validates declared state/certainty/output/continuation consistency and pending/background flags before serialization. Native session observation commits separately from stable framework session binding. Captured output and known termination are not erased by missing continuation.
