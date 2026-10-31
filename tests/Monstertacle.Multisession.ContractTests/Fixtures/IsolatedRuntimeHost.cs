using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

internal sealed class IsolatedRuntimeHost : IAsyncDisposable
{
    internal InMemorySessionStore Store { get; } = new();
    internal RuntimeOptions Options { get; }
    internal SessionRuntime Runtime { get; }
    internal IsolatedRuntimeHost(string name)
    {
        Options = new() { StateDirectory = Path.Combine(Path.GetTempPath(), "instance-" + name), Retention = new(true, null) };
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "native-" + name, null),
            Sending = (call, binding, observe, token) => ValueTask.FromResult(new ProviderReply(
                RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, name + ":" + call.Input, true, binding, null)),
        };
        Runtime = new(Store, new TestStateDirectoryLease(Options.StateDirectory), [new("provider", "controlled", 1, provider)],
            [new("profile", "provider", "model-" + name, ExecutionKind.ModelResponse,
                new("revision-" + name, new Dictionary<string, string> { ["owner"] = name }))]);
    }
    internal ValueTask StartAsync() => Runtime.StartAsync(Options, CancellationToken.None);
    internal ValueTask<SessionSnapshot> CreateSessionAsync() => Runtime.Sessions.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), CancellationToken.None);
    internal async Task<RequestSnapshot> SubmitAndCompleteAsync(Guid session, string input)
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        RequestReceipt receipt = await Runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session, input, TimeSpan.FromSeconds(5), null, false), deadline.Token);
        while (true)
        {
            RequestSnapshot current = (await Runtime.Requests.GetRequestAsync(receipt.RequestId, deadline.Token))!;
            if (current.State is not (RequestState.Queued or RequestState.Running)) { return current; }
            await Task.Delay(10, deadline.Token);
        }
    }
    public ValueTask DisposeAsync() => Runtime.DisposeAsync();
}
