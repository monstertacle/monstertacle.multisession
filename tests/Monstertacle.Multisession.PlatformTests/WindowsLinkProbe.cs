using System.Diagnostics;

namespace Monstertacle.Multisession.PlatformTests;

// Creates only test-owned links without changing symbolic-link privileges or global policy.
internal static class WindowsLinkProbe
{
    internal static async Task CreateAsync(string kind, string link, string target)
    {
        string script = Path.Combine(Path.GetDirectoryName(link)!, "link-" + Guid.NewGuid().ToString("N") + ".ps1");
        await File.WriteAllTextAsync(script, "param([string]$Kind,[string]$Link,[string]$Target)\nNew-Item -ItemType $Kind -Path $Link -Target $Target -ErrorAction Stop | Out-Null\n");
        ProcessStartInfo start = new("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "-NoProfile", "-File", script, kind, link, target }) { start.ArgumentList.Add(argument); }
        using Process process = new() { StartInfo = start };
        if (!process.Start()) { throw new InvalidOperationException("Native link fixture did not start."); }
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await output;
            string failure = await error;
            if (process.ExitCode != 0) { throw new InvalidOperationException("Native link fixture failed: " + failure); }
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            File.Delete(script);
        }
    }
}
