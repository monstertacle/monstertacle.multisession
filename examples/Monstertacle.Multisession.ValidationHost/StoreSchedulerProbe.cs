using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

// Isolated internal test protocol: never constrains the caller's process or a running host.
internal static class StoreSchedulerProbe
{
    internal const string Argument = "--store-scheduler-probe";
    private static readonly TimeSpan _startupLimit = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _probeWindow = TimeSpan.FromMilliseconds(500);
    private const int _isolatedWorkerCount = 1;

    internal static async Task<int> RunAsync(string directory)
    {
        await using NativeLease lease = NativeLease.CreateNew(directory);
        RuntimeOptions options = new() { StateDirectory = directory, Retention = new(true, null) };
        await using ISessionStore store = await SqliteStoreFactory.CreateNewAsync(lease, Guid.NewGuid(), options, CancellationToken.None);
        await store.StartAsync(lease, CancellationToken.None);
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread driver = new(() => Measure(store, completion)) { IsBackground = true };
        driver.Start();
        bool independent = await completion.Task;
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new { IndependentStoreRead = independent }));
        return independent ? 0 : 1;
    }

    private static void Measure(ISessionStore store, TaskCompletionSource<bool> completion)
    {
        ThreadPool.GetMinThreads(out int minimum, out int minimumIo);
        ThreadPool.GetMaxThreads(out int maximum, out int maximumIo);
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        using ManualResetEventSlim exited = new();
        try
        {
            if (!ThreadPool.SetMinThreads(_isolatedWorkerCount, minimumIo)
                || !ThreadPool.SetMaxThreads(_isolatedWorkerCount, maximumIo))
            { throw new InvalidOperationException("Isolated scheduler setup failed."); }
            ThreadPool.QueueUserWorkItem(_ => { entered.Set(); release.Wait(); exited.Set(); });
            if (!entered.Wait(_startupLimit)) { throw new TimeoutException("Isolated blocker did not start."); }
            Task<SessionSnapshot?> read = store.GetSessionAsync(Guid.NewGuid(), CancellationToken.None).AsTask();
            bool finished = SpinWait.SpinUntil(() => read.IsCompleted, _probeWindow);
            bool independent = finished && read.IsCompletedSuccessfully;
            completion.TrySetResult(independent);
        }
        catch (Exception failure) { completion.TrySetException(failure); }
        finally
        {
            release.Set();
            ThreadPool.SetMaxThreads(maximum, maximumIo);
            ThreadPool.SetMinThreads(minimum, minimumIo);
            if (entered.IsSet) { exited.Wait(_startupLimit); }
        }
    }
}
