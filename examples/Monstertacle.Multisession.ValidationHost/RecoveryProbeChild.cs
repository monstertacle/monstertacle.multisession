using System.Diagnostics;
using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

// Owns only its explicitly marked child; no repository lookup or process discovery.
internal sealed class RecoveryProbeChild(Process process, CrashStateManifest manifest) : IAsyncDisposable
{
    internal CrashStateManifest Manifest { get; } = manifest;
    internal static async Task<RecoveryProbeChild> StartAsync(ValidationRunState state)
    {
        string nonce = Guid.NewGuid().ToString("N");
        await state.Lease.PrepareFileAsync(CrashProtocol.MarkerFileName, StateFileMode.CreateNew, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(state.Directory, CrashProtocol.MarkerFileName), nonce);
        await state.Lease.DisposeAsync();
        ProcessStartInfo start = new("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { typeof(RecoveryProbeChild).Assembly.Location, CrashProtocol.NativeWorkerArgument, state.Directory, nonce, CrashProtocol.RecoveryPoint })
        {
            start.ArgumentList.Add(argument);
        }
        Process child = new() { StartInfo = start };
        if (!child.Start()) { child.Dispose(); throw new ValidationProbeException("RecoveryWorkerDidNotStart"); }
        _ = child.StandardError.ReadToEndAsync();
        try
        {
            string? line = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
            CrashSignal? signal = line is null ? null : JsonSerializer.Deserialize<CrashSignal>(line);
            ValidationCheck.Require(signal?.ProcessId == child.Id && signal.Barrier == CrashProtocol.RecoveryBarrier
                && signal.State?.QueuedA.Count == 3, "RecoveryWorkerDidNotReachBarrier");
            return new(child, signal!.State!);
        }
        catch { await EndAsync(child); child.Dispose(); throw; }
    }
    internal Task KillAsync() => EndAsync(process);
    private static async Task EndAsync(Process child)
    {
        if (!child.HasExited) { child.Kill(entireProcessTree: true); }
        await ProcessExitWait.WaitAsync(child, TimeSpan.FromSeconds(30));
    }
    public async ValueTask DisposeAsync()
    {
        try { await EndAsync(process); }
        finally { process.Dispose(); }
    }
}
