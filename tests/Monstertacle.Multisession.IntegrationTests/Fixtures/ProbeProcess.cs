using System.Diagnostics;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

internal sealed record ProbeProcessResult(int ExitCode, string Output, string Error);

// A bounded owned process; streams are drained concurrently and abandoned children are killed.
internal static class ProbeProcess
{
    internal static async Task<ProbeProcessResult> RunAsync(ProcessStartInfo start)
    {
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using Process process = new() { StartInfo = start };
        if (!process.Start()) { throw new InvalidOperationException("Probe process did not start."); }
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
            return new(process.ExitCode, await output, await error);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(true); }
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
