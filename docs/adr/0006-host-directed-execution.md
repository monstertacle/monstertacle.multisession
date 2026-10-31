# Host-directed execution and canonical identities

Status: accepted for isolated implementation; producer and host release gates remain pending.

## Context

A host must persist an invocation intent before crossing into the SDK. Library-generated session/request IDs leave a receipt-loss window in which the host cannot distinguish the original admission from a replacement. Unconditional workers also prevent safe inspection/reconciliation on host startup, and unconditional shutdown cancellation changes unstarted host work.

The contrary requirements and architecture reviews for SharpClaw feature004 identified these concrete boundary gaps. Feature006 records the source evidence and disposition. Implementation cost is not a decision criterion.

## Decision

Canonical session creation and request admission require caller-supplied GUIDs through both facades and CQRS commands. Duplicate IDs refuse transactionally before a second external operation. An empty ID is invalid. There are no generated-ID compatibility methods. An existing receipt is read by its original identity rather than re-submitted.

Store format2 persists immutable Automatic or HostControlled dispatch authority. HostControlled startup prepares recovery and reads without starting queue workers; shutdown preserves unstarted queued requests. Explicit dispatch must name the selected eligible FIFO head. Selection and the expected-identity check occur in the same authoritative reservation transaction, preserving session/workspace/capacity exclusion. Explicit work participates in the existing lifecycle cancellation and drain before store/lease disposal. No secondary request ledger is introduced.

Microsoft Agent Framework remains an unchanged external dependency. Invocation/outcome integration data belongs to Monstertacle and uses public framework extension points. This decision grants no authority to modify upstream contracts, deploy running hosts or migrate active stores.

## Alternatives and consequences

An outer mapping ledger would retain a cross-store crash window and duplicate identity authority. Automatic background dispatch would violate host admission ownership. Starting an external agent directly from SharpClaw would bypass the SDK reservation and recovery authority. Those alternatives are rejected on correctness and ownership grounds.

Existing format1 stores are not silently upgraded, reset or deleted. They remain available to the matching old reader. A prior-package refusal probe, native Windows qualification, typed native outcomes, exact recovered-submission fingerprints and real SharpClaw runtime consumption remain release requirements in feature006/feature004; local identity/dispatch tests alone do not satisfy them.
