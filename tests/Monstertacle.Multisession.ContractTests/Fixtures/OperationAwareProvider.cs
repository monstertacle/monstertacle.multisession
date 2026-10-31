using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

internal sealed class OperationAwareProvider : ISessionProvider, IExecutionRecoveryProvider
{
    internal ControlledProvider Inner { get; init; } = new();
    internal int LegacyReopenCalls { get; private set; }
    internal int RecoveryCalls { get; private set; }
    internal Func<ProviderBinding, string?, CancellationToken, ValueTask<ProviderBinding>>? Recovering { get; init; }
    public ProviderCapabilities Capabilities => Inner.Capabilities with { SupportsInspection = true, RequiresOperationAwareRecovery = true };
    public ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken) => Inner.OpenAsync(context, cancellationToken);
    public ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken)
    {
        LegacyReopenCalls++;
        return ValueTask.FromResult(binding);
    }
    public ValueTask<ProviderBinding> ReopenAfterExecutionAsync(ProviderSessionContext context, ProviderBinding knownBinding, string? operationReference, CancellationToken cancellationToken)
    {
        RecoveryCalls++;
        if (Recovering is not null) { return Recovering(knownBinding, operationReference, cancellationToken); }
        if (operationReference is null) { throw new SessionOperationException(new PublicError(ErrorCode.UnsupportedProviderAction, ExecutionCertainty.Unknown)); }
        return ValueTask.FromResult(knownBinding with { ContinuationState = "verified-new-head" });
    }
    public ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding, Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
        => Inner.SendAsync(request, binding, observe, cancellationToken);
    public ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => Inner.InspectAsync(context, operationId, cancellationToken);
    public ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => Inner.CancelAsync(context, operationId, cancellationToken);
}
