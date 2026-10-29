using System.Diagnostics;
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
    public CrashSignal? Signal { get; private set; }

    public static async Task<CrashHarness> StartAsync(IStateDirectoryLease lease, string point)
    {
        string stateDirectory = lease.StateDirectory;
        string token = Guid.NewGuid().ToString("N");
        await lease.PrepareFileAsync(CrashProtocol.MarkerFileName, StateFileMode.CreateNew, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(stateDirectory, CrashProtocol.MarkerFileName), token);
        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = stateDirectory,
        };
        start.ArgumentList.Add(ValidationHostBuild.WorkerPath());
        start.ArgumentList.Add(CrashProtocol.WorkerArgument);
        start.ArgumentList.Add(stateDirectory);
        start.ArgumentList.Add(token);
        start.ArgumentList.Add(point);
        Process child = new() { StartInfo = start };
        CrashHarness harness = new(child);
        try
        {
            if (!child.Start()) { throw new InvalidOperationException("Crash worker could not start."); }
            string? line = await child.StandardOutput.ReadLineAsync().WaitAsync(_processLimit);
            harness.Signal = line is null ? null : JsonSerializer.Deserialize<CrashSignal>(line);
            string expectedBarrier = point == CrashProtocol.JournalPoint ? CrashProtocol.JournalBarrier : CrashProtocol.RecoveryBarrier;
            if (harness.Signal is null || harness.Signal.Barrier != expectedBarrier || harness.Signal.ProcessId != child.Id)
            {
                throw new InvalidOperationException($"Crash worker did not reach the expected barrier: {line}");
            }
            return harness;
        }
        catch { await harness.DisposeAsync(); throw; }
    }

    public async Task KillAsync()
    {
        if (!process.HasExited) { process.Kill(true); }
        await ProcessExitWait.WaitAsync(process, _processLimit);
        await WaitForFileReleaseAsync(process.StartInfo.WorkingDirectory);
    }

    private static async Task WaitForFileReleaseAsync(string directory)
    {
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
