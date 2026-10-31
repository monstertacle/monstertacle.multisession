using System.Net;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Conversations;
using Monstertacle.Multisession.Providers.Interactions;

namespace Monstertacle.Multisession.ValidationHost;

internal static class NativeContextProbeFlow
{
    private static readonly string[] _conversationInputs = ["conversation-first", "conversation-follow"];
    private static readonly string[] _interactionInputs = ["interaction-first", "interaction-follow"];
    internal static async Task ExecuteAsync(ValidationRunState state)
    {
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using NativeWireFixture conversations = new(true, release.Task);
        using NativeWireFixture interactions = new(false, release.Task);
        using ScriptedHttpHandler rejected = new((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/v1/conversations"
            ? ScriptedHttpHandler.Json("""{"object":"conversation","id":"conv_context_limit"}""")
            : ScriptedHttpHandler.Json("""{"error":{"code":"context_length_exceeded","message":"private context details"}}""", HttpStatusCode.BadRequest)));
        using ConversationSessionProvider conversation = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "fixture-conversation" }, new FixtureCredentials(), conversations.Handler);
        using InteractionSessionProvider interaction = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "fixture-interaction" }, new FixtureCredentials(), interactions.Handler);
        using ConversationSessionProvider contextLimit = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "fixture-limit" }, new FixtureCredentials(), rejected);
        await using ProbeHost host = await ProbeHost.StartAsync(state,
            [new("conversation", "openai-responses", 1, conversation), new("interaction", "gemini-interactions", 1, interaction), new("limit", "openai-responses", 1, contextLimit)]);
        SessionSnapshot a = await host.CreateAsync("conversation");
        SessionSnapshot b = await host.CreateAsync("interaction");
        RequestReceipt firstA = await host.SubmitAsync(a.SessionId, "conversation-first");
        RequestReceipt firstB = await host.SubmitAsync(b.SessionId, "interaction-first");
        try
        {
            await Task.WhenAll(conversations.Entered.Task, interactions.Entered.Task).WaitAsync(TimeSpan.FromSeconds(5));
            ValidationCheck.Require((await host.Store.GetRuntimeStatisticsAsync(CancellationToken.None)).OccupiedReservations == 2, "NativeContractsDidNotOverlap");
        }
        finally { release.TrySetResult(); }
        await RequireSuccessAsync(host, firstA, "conversation-first");
        await RequireSuccessAsync(host, firstB, "interaction-first");
        RequestReceipt followA = await host.SubmitAsync(a.SessionId, "conversation-follow");
        RequestReceipt followB = await host.SubmitAsync(b.SessionId, "interaction-follow");
        await RequireSuccessAsync(host, followA, "conversation-follow");
        await RequireSuccessAsync(host, followB, "interaction-follow");
        ValidationCheck.Require(conversations.Inputs.SequenceEqual(_conversationInputs)
            && interactions.Inputs.SequenceEqual(_interactionInputs), "NativeInputWasReplayedOrChanged");
        await RequireHeadsAsync(host, a, b);
        SessionSnapshot limited = await host.CreateAsync("limit");
        RequestSnapshot failed = await host.EndAsync((await host.SubmitAsync(limited.SessionId, "context-boundary")).RequestId);
        ValidationCheck.Require(failed.State == RequestState.Failed && failed.Result?.Error?.Code == ErrorCode.ProviderContextLimit
            && failed.Result?.Output == string.Empty && rejected.Calls == 2, "NativeContextErrorWasChangedOrRetried");
        await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }

    private static async Task RequireSuccessAsync(ProbeHost host, RequestReceipt receipt, string input)
    {
        RequestSnapshot request = await host.EndAsync(receipt.RequestId);
        ValidationCheck.Require(request.State == RequestState.Succeeded && request.Result?.Output == input, "NativeTurnFailed");
    }

    private static async Task RequireHeadsAsync(ProbeHost host, SessionSnapshot a, SessionSnapshot b)
    {
        SessionSnapshot currentA = await host.Runtime.Sessions.GetSessionAsync(a.SessionId, CancellationToken.None) ?? throw new ValidationProbeException("MissingNativeSession");
        SessionSnapshot currentB = await host.Runtime.Sessions.GetSessionAsync(b.SessionId, CancellationToken.None) ?? throw new ValidationProbeException("MissingNativeSession");
        ValidationCheck.Require(currentA.Binding?.SessionReference == "conv_runtime" && currentA.Binding == a.Binding
            && currentB.Binding?.SessionReference == "interaction_runtime_1" && currentB.Binding.ContinuationState == "interaction_runtime_2", "NativeBindingWasReplaced");
    }
}
