using System.Diagnostics;

namespace Monstertacle.Multisession.PlatformTests;

// Bounded native fixture commands; callers supply only paths freshly owned by their test.
internal static class LinuxOwnershipProbe
{
    internal static async Task<string> RunAsync(string command, params string[] arguments)
    {
        ProcessStartInfo start = new(command) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        using Process process = new() { StartInfo = start };
        if (!process.Start()) { throw new InvalidOperationException("Native ownership fixture did not start."); }
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            string result = await output;
            string failure = await error;
            if (process.ExitCode != 0) { throw new InvalidOperationException("Native ownership fixture failed: " + failure); }
            return result.Trim();
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
