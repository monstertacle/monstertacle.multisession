using System.Diagnostics;

namespace Monstertacle.Multisession.ValidationHost;

// Process cleanup must finish before another native owner reopens the same files.
internal static class ProcessExitWait
{
    internal static async Task WaitAsync(Process process, TimeSpan timeout)
        => await process.WaitForExitAsync().WaitAsync(timeout);
}
