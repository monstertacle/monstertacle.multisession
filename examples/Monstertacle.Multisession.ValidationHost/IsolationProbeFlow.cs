using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal static class IsolationProbeFlow
{
    internal static async Task ExecuteAsync(ValidationRunState state)
    {
        await using ValidationRunState secondary = new(Guid.NewGuid(), NativeLease.CreateNew(Path.Combine(state.Directory, "secondary")));
        ProbeProvider left = new("left-adapter", new(), (call, binding, _) => ValueTask.FromResult(ProbeReplies.Success(call, binding, "left:" + call.Input)));
        ProbeProvider right = new("right-adapter", new(), (call, binding, _) => ValueTask.FromResult(ProbeReplies.Success(call, binding, "right:" + call.Input)));
        await using ProbeHost a = await ProbeHost.StartAsync(state, [new("shared", "left-adapter", 1, left)], configuredProfiles: [Profile("left")]);
        await using ProbeHost b = await ProbeHost.StartAsync(secondary, [new("shared", "right-adapter", 1, right)], configuredProfiles: [Profile("right")]);
        SessionSnapshot sessionA = await a.CreateAsync("shared");
        SessionSnapshot sessionB = await b.CreateAsync("shared");
        ValidationCheck.Require(sessionA.Context.ModelId == "opaque-left" && sessionB.Context.ModelId == "opaque-right"
            && sessionA.Context.Settings["owner"] == "left" && sessionB.Context.Settings["owner"] == "right", "SharedProviderConfiguration");
        RequestReceipt requestA = await a.SubmitAsync(sessionA.SessionId, "first");
        RequestReceipt requestB = await b.SubmitAsync(sessionB.SessionId, "first");
        ValidationCheck.Require((await a.EndAsync(requestA.RequestId)).Result?.Output == "left:first"
            && (await b.EndAsync(requestB.RequestId)).Result?.Output == "right:first", "CrossedInstanceOutput");
        ValidationCheck.Require(await a.Runtime.Sessions.GetSessionAsync(sessionB.SessionId, CancellationToken.None) is null
            && await b.Runtime.Sessions.GetSessionAsync(sessionA.SessionId, CancellationToken.None) is null
            && await b.Runtime.Requests.GetRequestAsync(requestA.RequestId, CancellationToken.None) is null, "ForeignIdentityWasVisible");
        await a.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        RequestReceipt later = await b.SubmitAsync(sessionB.SessionId, "after-other-stop");
        ValidationCheck.Require((await b.EndAsync(later.RequestId)).Result?.Output == "right:after-other-stop", "OtherStopAffectedInstance");
        await b.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }
    private static AgentProfile Profile(string owner)
        => new("shared", "shared", "opaque-" + owner, ExecutionKind.ModelResponse,
            new("revision-" + owner, new Dictionary<string, string> { ["owner"] = owner }));
}
