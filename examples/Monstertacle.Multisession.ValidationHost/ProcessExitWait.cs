using System.Diagnostics;

namespace Monstertacle.Multisession.ValidationHost;

// Process cleanup must finish before another native owner reopens the same files.
internal static class ProcessExitWait
{
    internal static async Task WaitAsync(Process process, TimeSpan timeout)
    {
        if (OperatingSystem.IsWindows())
        {
            // HasExited may expose an exit code before the kernel process handle signals.
            bool signaled = await Task.Run(() => process.WaitForExit(timeout));
            if (!signaled) { throw new TimeoutException("Native process termination did not complete within its deadline."); }
            return;
        }
        await process.WaitForExitAsync().WaitAsync(timeout);
    }
}
