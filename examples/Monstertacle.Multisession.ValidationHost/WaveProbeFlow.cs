using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal static class WaveProbeFlow
{
    internal static async Task ExecuteAsync(ValidationRunState state, string scenario)
    {
        ProbeTrace trace = new();
        ProbeBarrier barrier = new(2);
        async ValueTask<ProviderReply> SendAsync(ProviderCall call, ProviderBinding binding, CancellationToken token)
        {
            await barrier.WaitAsync(token);
            return call.Input.StartsWith("fail-", StringComparison.Ordinal) ? ProbeReplies.Failed(binding) : ProbeReplies.Success(call, binding);
        }
        ProbeProvider a = new("probe-alpha", trace, SendAsync);
        ProbeProvider b = new("probe-beta", trace, SendAsync);
        await using ProbeHost host = await ProbeHost.StartAsync(state, [new("alpha", "probe-alpha", 1, a), new("beta", "probe-beta", 1, b)]);
        List<SessionSnapshot> sessions = [];
        for (int index = 0; index < 10; index++) { sessions.Add(await host.CreateAsync(index % 2 == 0 ? "alpha" : "beta")); }
        List<AcceptedProbeRequest> accepted = [];
        int rounds = scenario == "routing" ? 10 : scenario == "failures" ? 1 : 2;
        try
        {
            for (int round = 0; round < rounds; round++)
            {
                for (int index = 0; index < sessions.Count; index++)
                {
                    bool failure = scenario == "failures" && index < 5;
                    string input = (failure ? "fail-" : "input-") + index + "-" + round;
                    RequestReceipt receipt = await host.SubmitAsync(sessions[index].SessionId, input);
                    accepted.Add(new(sessions[index].SessionId, receipt, input, failure ? RequestState.Failed : RequestState.Succeeded));
                    if (accepted.Count == 2)
                    {
                        await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                        ValidationCheck.Require(trace.Peak == 2, "IndependentSendsDidNotOverlap");
                        barrier.Release.TrySetResult();
                    }
                }
            }
        }
        catch (SessionOperationException failure) { throw new ValidationProbeException("WaveAdmission_" + failure.Failure.Code); }
        finally { barrier.Release.TrySetResult(); }
        foreach (AcceptedProbeRequest item in accepted)
        {
            RequestSnapshot request = await host.EndAsync(item.Receipt.RequestId);
            ValidationCheck.Require(request.State == item.Expected && request.Certainty == ExecutionCertainty.ConfirmedEnded
                && request.SessionId == item.SessionId && request.Sequence == item.Receipt.Sequence, "WrongTerminalIdentity");
            if (item.Expected == RequestState.Succeeded) { ValidationCheck.Require(request.Result?.Output == item.Input, "ChangedOutput"); }
        }
        foreach (SessionSnapshot session in sessions)
        {
            AcceptedProbeRequest[] expected = accepted.Where(item => item.SessionId == session.SessionId).ToArray();
            Page<HistoryEntry> history = await host.Runtime.Sessions.ReadHistoryAsync(session.SessionId, 100, null, CancellationToken.None);
            ValidationCheck.Require(history.Items.Select(item => item.RequestId).SequenceEqual(expected.Select(item => item.Receipt.RequestId))
                && history.Items.Select(item => item.Input).SequenceEqual(expected.Select(item => item.Input)), "WrongSessionFifoHistory");
            ProbeInvocation[] calls = trace.Calls.Where(item => item.Call.Session.SessionId == session.SessionId).ToArray();
            ValidationCheck.Require(calls.Select(item => item.Call.Input).SequenceEqual(expected.Select(item => item.Input))
                && calls.All(item => item.Call.Session.ModelId == session.Context.ModelId), "CrossedExecutionContext");
        }
        ValidationCheck.Require(trace.Calls.Count == accepted.Count, "RepeatedOrMissingSend");
        await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }
}

internal sealed record AcceptedProbeRequest(Guid SessionId, RequestReceipt Receipt, string Input, RequestState Expected);
