using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ValidationHost;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// A real process boundary; cleanup always kills a child that has not already exited.
internal sealed class CrashHarness(Process process) : IAsyncDisposable
{
    private static readonly TimeSpan _processLimit = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _fileReleasePollInterval = TimeSpan.FromMilliseconds(20);
    private const int _sharingViolation = 32;
    private const int _lockViolation = 33;
    private const string _databaseFileName = "multisession.db";
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);
    public CrashSignal? Signal { get; private set; }

    // The measured worker steps of the signal, for test output on every native run.
    public string StepsReport() => "worker steps: " + JsonSerializer.Serialize(Signal?.Steps ?? []);

    public static async Task<CrashHarness> StartAsync(IStateDirectoryLease lease, string point)
    {
        string stateDirectory = lease.StateDirectory;
        string token = Guid.NewGuid().ToString("N");
        await lease.PrepareFileAsync(CrashProtocol.MarkerFileName, StateFileMode.CreateNew, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(stateDirectory, CrashProtocol.MarkerFileName), token);
        return await StartWorkerAsync(WorkerStart(stateDirectory, CrashProtocol.WorkerArgument, stateDirectory, token, point), point, _processLimit);
    }

    // The validation-host worker started in the state directory with both output streams redirected.
    internal static ProcessStartInfo WorkerStart(string stateDirectory, params string[] arguments)
    {
        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = stateDirectory,
        };
        start.ArgumentList.Add(ValidationHostBuild.WorkerPath());
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        return start;
    }

    // Waits up to the limit for the worker's first line; production uses the 30 s limit, T023's owned controls a short one.
    internal static async Task<CrashHarness> StartWorkerAsync(ProcessStartInfo start, string point, TimeSpan limit)
    {
        string stateDirectory = start.WorkingDirectory;
        Process child = new() { StartInfo = start };
        FirstLineWait firstLine = new(child, limit); // T023: standard error read from the start; an attributed timeout
        CrashHarness harness = new(child);
        try
        {
            if (!child.Start()) { throw new InvalidOperationException("Crash worker could not start."); }
            string? line = await firstLine.ReadAsync(harness.KillAsync);
            harness.Signal = line is null ? null : JsonSerializer.Deserialize<CrashSignal>(line);
            string expectedBarrier = point == CrashProtocol.JournalPoint ? CrashProtocol.JournalBarrier : CrashProtocol.RecoveryBarrier;
            if (harness.Signal is null || harness.Signal.Barrier != expectedBarrier || harness.Signal.ProcessId != child.Id)
            {
                // The worker is stopped first; only then is a reported uncertain candidate looked up, never sent again.
                await harness.KillAsync();
                throw new InvalidOperationException($"Crash worker did not reach the expected barrier: {line}{DescribeCandidate(Path.Combine(stateDirectory, _databaseFileName), ReportedCandidate(line))}");
            }
            return harness;
        }
        catch (Exception primary)
        {
            // The start failure stays the thrown one; a failure to end the child is reported with it, never instead of it.
            try { await harness.DisposeAsync(); }
            catch (Exception cleanup) { throw new AggregateException("Ending the crash worker failed after its start failed.", primary, cleanup); }
            throw;
        }
    }

    // Read-only lookup of a candidate the worker reported, after the worker is gone: never a resubmission.
    internal static string DescribeCandidate(string databasePath, Guid? requestId)
    {
        if (requestId is not { } candidate) { return string.Empty; }
        string id = candidate.ToString("D");
        object? state = StoreProbe.Scalar(databasePath, "SELECT State FROM Requests WHERE RequestId='" + id + "'");
        if (state is null) { return " candidate " + id + ": absent"; }
        object? certainty = StoreProbe.Scalar(databasePath, "SELECT Certainty FROM Requests WHERE RequestId='" + id + "'");
        return " candidate " + id + ": state=" + (RequestState)Convert.ToInt32(state, CultureInfo.InvariantCulture)
            + " certainty=" + (ExecutionCertainty)Convert.ToInt32(certainty, CultureInfo.InvariantCulture);
    }

    private static Guid? ReportedCandidate(string? line)
    {
        try { return line is null ? null : JsonSerializer.Deserialize<CrashFailure>(line, _web)?.RequestId; }
        catch (JsonException) { return null; }
    }

    public async Task KillAsync()
    {
        if (!process.HasExited) { process.Kill(true); }
        await ProcessExitWait.WaitAsync(process, _processLimit);
        await WaitForFileReleaseAsync(process.StartInfo.WorkingDirectory);
    }

    private static async Task WaitForFileReleaseAsync(string directory)
    {
        // A worker that ended before it created its store holds no file: nothing to wait for (T023; this no longer replaces the timeout).
        if (!File.Exists(Path.Combine(directory, _databaseFileName))) { return; }
        using CancellationTokenSource deadline = new(_processLimit);
        while (true)
        {
            try
            {
                using FileStream database = new(Path.Combine(directory, "multisession.db"), FileMode.Open, FileAccess.Read, FileShare.Read);
                string journal = Path.Combine(directory, "multisession.db-journal");
                if (File.Exists(journal)) { using FileStream released = new(journal, FileMode.Open, FileAccess.Read, FileShare.Read); }
                return;
            }
            catch (IOException failure) when (OperatingSystem.IsWindows()
                && (failure.HResult & ushort.MaxValue) is _sharingViolation or _lockViolation)
            {
                await Task.Delay(_fileReleasePollInterval, deadline.Token);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await KillAsync();
        process.Dispose();
    }

}
