using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.ValidationHost;

// The crash worker's timed setup operations and its barrier signal, each measured by the same step clock.
internal sealed class CrashSetupSteps(CrashStepClock clock, RequestAdmission admission, RequestDispatcher dispatcher)
{
    internal Task<RequestReceipt> SubmitAsync(string step, SessionSnapshot session, string input)
        => clock.MeasureAsync(step, () => admission.SubmitAsync(Guid.NewGuid(), new(session.SessionId, input, TimeSpan.FromMinutes(10), session.Context.ExecutionContext.WorkspaceKey, true), CancellationToken.None).AsTask());

    internal Task DispatchAsync(string step) => clock.MeasureAsync(step, async () =>
    {
        if (!await dispatcher.TryDispatchAsync(CancellationToken.None)) { throw new InvalidOperationException("Missing setup dispatch."); }
    });

    internal async Task<int> HoldAsync(CrashStateManifest manifest)
    {
        await SignalAsync(manifest);
        await Task.Delay(Timeout.InfiniteTimeSpan);
        return 2;
    }

    internal async Task SignalAsync(CrashStateManifest manifest)
    {
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new CrashSignal(CrashProtocol.RecoveryBarrier, Environment.ProcessId, DateTimeOffset.UtcNow)
        { State = manifest, Steps = [.. clock.Steps] }));
        await Console.Out.FlushAsync();
    }
}
