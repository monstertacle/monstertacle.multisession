using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal static class LimitsProbeFlow
{
    internal static async Task ExecuteAsync(ValidationRunState state)
    {
        ProbeTrace trace = new();
        ProbeProvider provider = new("probe", trace, (call, binding, _) => ValueTask.FromResult(
            ProbeReplies.Success(call, binding, call.Input switch { "o6" => "ä🙂", "o7" => "ä🙂x", _ => call.Input })));
        RuntimeOptions options = new() { StateDirectory = state.Directory, Retention = new(true, null), MaxConcurrentRequests = 2, MaxInputBytes = 6, MaxOutputBytes = 6 };
        await using ProbeHost host = await ProbeHost.StartAsync(state, [new("probe", "probe", 1, provider)], options);
        SessionSnapshot session = await host.CreateAsync("probe");
        await RejectAsync(host, session.SessionId, "", ErrorCode.InvalidInput);
        await RejectAsync(host, session.SessionId, new string((char)0xd800, 1), ErrorCode.InvalidInput);
        await RejectAsync(host, session.SessionId, "ä🙂x", ErrorCode.InputLimitExceeded);
        RequestSnapshot atInput = await host.EndAsync((await host.SubmitAsync(session.SessionId, "ä🙂")).RequestId);
        RequestSnapshot atOutput = await host.EndAsync((await host.SubmitAsync(session.SessionId, "o6")).RequestId);
        ValidationCheck.Require(atInput.State == RequestState.Succeeded && atOutput.State == RequestState.Succeeded
            && atInput.Result?.Output == "ä🙂" && atOutput.Result?.Output == "ä🙂", "InclusiveUtf8LimitFailed");
        Page<HistoryEntry> before = await host.Runtime.Sessions.ReadHistoryAsync(session.SessionId, 100, null, CancellationToken.None);
        RequestSnapshot above = await host.EndAsync((await host.SubmitAsync(session.SessionId, "o7")).RequestId);
        ValidationCheck.Require(above.State == RequestState.Failed && above.Result is { Output: "", OutputComplete: false }
            && above.Result.Error?.Code == ErrorCode.OutputLimitExceeded, "OutputWasTruncatedOrReportedSuccessful");
        Page<HistoryEntry> after = await host.Runtime.Sessions.ReadHistoryAsync(session.SessionId, 100, null, CancellationToken.None);
        ValidationCheck.Require(after.Items.Count == 3 && before.Items.SequenceEqual(after.Items.Take(2)) && trace.Calls.Count == 3, "EarlierHistoryChanged");
        await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }
    private static async Task RejectAsync(ProbeHost host, Guid session, string input, ErrorCode expected)
    {
        try { await host.SubmitAsync(session, input); }
        catch (SessionOperationException failure) when (failure.Failure.Code == expected && failure.Failure.AdmissionStatus == AdmissionStatus.NotAccepted) { return; }
        throw new ValidationProbeException("InvalidInputWasAcceptedOrMisclassified");
    }
}
