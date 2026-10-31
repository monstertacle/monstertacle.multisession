using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession;

// Executes bounded scheduling; the lifecycle still owns workers and fault transitions.
internal static class RuntimeWorker
{
    internal static async Task RunAsync(RequestDispatcher dispatcher, SemaphoreSlim signal, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (await dispatcher.TryDispatchAsync(token)) { continue; }
                await signal.WaitAsync(RuntimeDefaults.SchedulerPollInterval, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (SessionOperationException failure) when (failure.Failure.RequestId is not null && failure.Failure.Code != ErrorCode.StorageUnavailable)
            {
                // A persisted local request failure is observable through request/recovery reads.
            }
        }
    }
}
