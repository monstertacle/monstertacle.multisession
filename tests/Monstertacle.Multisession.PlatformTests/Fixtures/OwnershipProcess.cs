using System.Diagnostics;
using System.Text.Json;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.ValidationHost;

namespace Monstertacle.Multisession.PlatformTests.Fixtures;

internal sealed class OwnershipProcess(Process process) : IAsyncDisposable
{
    private static readonly TimeSpan _limit = TimeSpan.FromSeconds(20);
    internal OwnershipSignal? Signal { get; private set; }
    internal bool KernelExitSignaled => process.WaitForExit(0);
    internal static async Task<OwnershipProcess> StartAsync(string path, string token, Guid trustScope)
    {
        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        foreach (string argument in new[] { ValidationHostBuild.WorkerPath(), OwnershipProtocol.WorkerArgument, path, token, trustScope.ToString("D") })
        {
            start.ArgumentList.Add(argument);
        }
        Process process = new() { StartInfo = start };
        OwnershipProcess owner = new(process);
        try
        {
            if (!process.Start()) { throw new InvalidOperationException("Ownership worker could not start."); }
            string? line = await process.StandardOutput.ReadLineAsync().WaitAsync(_limit);
            owner.Signal = line is null ? null : JsonSerializer.Deserialize<OwnershipSignal>(line);
            if (owner.Signal is null || owner.Signal.ProcessId != process.Id)
            {
                throw new InvalidOperationException("Ownership worker did not reach its native lease barrier.");
            }
            return owner;
        }
        catch { await owner.DisposeAsync(); throw; }
    }

    internal async Task KillAsync()
    {
        if (!process.HasExited) { process.Kill(true); }
        await ProcessExitWait.WaitAsync(process, _limit);
    }
    public async ValueTask DisposeAsync()
    {
        await KillAsync();
        process.Dispose();
    }
}
