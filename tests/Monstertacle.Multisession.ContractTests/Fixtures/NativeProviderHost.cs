using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Providers.Conversations;
using Monstertacle.Multisession.Providers.Interactions;
using Monstertacle.Multisession.UnitTests.Fixtures;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

internal sealed class NativeProviderHost : IAsyncDisposable
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CompletionBarrier _completion = new(9);
    private readonly IDisposable[] _providers;
    private static readonly string[] _profiles = ["conversation", "interaction", "external"];

    internal NativeProviderHost()
    {
        Conversations = new(true, _release.Task);
        Interactions = new(false, _release.Task);
        External = new(_release.Task);
        ConversationSessionProvider conversation = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "conversation-reference" }, new FixtureCredentials(), Conversations.Handler);
        InteractionSessionProvider interaction = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "interaction-reference" }, new FixtureCredentials(), Interactions.Handler);
        _providers = [conversation, interaction];
        Store = new() { DispatchState = new() { Completed = _completion.Record } };
        Runtime = new(Store, new TestStateDirectoryLease(Path.GetTempPath()),
            [new("conversation", "openai-responses", 1, conversation), new("interaction", "gemini-interactions", 1, interaction), new("external", "third-contract", 7, External)],
            [new("conversation", "conversation", "opaque-conversation", ExecutionKind.ModelResponse, new("revision-one", new Dictionary<string, string> { ["instructions"] = "fixed instruction" })),
             new("interaction", "interaction", "opaque-interaction", ExecutionKind.ModelResponse, new("revision-two", new Dictionary<string, string> { ["system_instruction"] = "fixed instruction" })),
             new("external", "external", "opaque-external-agent", ExecutionKind.AgentTask, new("revision-three", new Dictionary<string, string>()))]);
    }
    internal SessionRuntime Runtime { get; }
    internal InMemorySessionStore Store { get; }
    internal NativeWireFixture Conversations { get; }
    internal NativeWireFixture Interactions { get; }
    internal ThirdContractProvider External { get; }
    internal Task Completed => _completion.Completed.Task;
    internal Task Entered => Task.WhenAll(Conversations.Entered.Task, Interactions.Entered.Task, External.Entered.Task);
    internal void Release() => _release.TrySetResult();
    internal async Task<IReadOnlyList<ProviderRun>> StartAndScheduleAsync()
    {
        await Runtime.StartAsync(new() { StateDirectory = Path.GetTempPath(), Retention = new(true, null), MaxConcurrentRequests = 3 }, CancellationToken.None);
        List<ProviderRun> runs = [];
        foreach (string profile in _profiles)
        {
            SessionExecutionContext context = profile == "external" ? new(Path.GetTempPath(), "external-workspace") : new(null, null);
            SessionSnapshot session = await Runtime.Sessions.CreateSessionAsync(profile, context, CancellationToken.None);
            List<string> inputs = [];
            List<RequestReceipt> receipts = [];
            for (int index = 0; index < 3; index++)
            {
                string input = $"{profile}-new-input-{index}";
                inputs.Add(input);
                receipts.Add(await Runtime.Requests.SubmitAsync(new(session.SessionId, input, TimeSpan.FromSeconds(10), null, false), CancellationToken.None));
            }
            runs.Add(new(session, inputs, receipts));
        }
        return runs;
    }
    public async ValueTask DisposeAsync()
    {
        Release();
        await Runtime.DisposeAsync();
        foreach (IDisposable provider in _providers) { provider.Dispose(); }
        Conversations.Dispose();
        Interactions.Dispose();
    }
}
