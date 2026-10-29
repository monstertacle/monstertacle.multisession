using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

internal sealed class AdmissionProvider : ISessionProvider
{
    public Action<ProviderSessionContext>? Opening { get; set; }
    public ProviderCapabilities Capabilities { get; } = new(true, true, false, false, false);
    public ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        Opening?.Invoke(context);
        return ValueTask.FromResult(new ProviderBinding("fixture-adapter", 7, "native-" + context.SessionId.ToString("N"), "confirmed"));
    }
    public ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding, Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => throw new NotImplementedException();
}
