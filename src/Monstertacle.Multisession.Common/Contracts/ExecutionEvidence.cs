namespace Monstertacle.Multisession.Contracts;

/// <summary>Acknowledged dispatch observations linked to one request; evidence only, never resume or retry authority.</summary>
/// <param name="InvocationPrepared">Whether the adapter durably recorded invocation intent before calling the external agent.</param>
/// <param name="OperationReference">Acknowledged external operation identity, when the agent reported one.</param>
/// <param name="NativeSessionReference">Acknowledged native session identity, independent from the stable binding.</param>
public sealed record ExecutionEvidence(bool InvocationPrepared, string? OperationReference, string? NativeSessionReference);
