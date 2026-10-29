using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Monstertacle.Multisession.PlatformTests;

// Duplicates only the lock descriptor for the test's exact private state path.
[SupportedOSPlatform("linux")]
internal static partial class LinuxDescriptorProbe
{
    internal static int DuplicateLock(string stateDirectory)
    {
        string path = Path.Combine(stateDirectory, ".multisession.lock");
        foreach (string entry in Directory.EnumerateFiles("/proc/self/fd"))
        {
            try
            {
                if (new FileInfo(entry).LinkTarget != path) { continue; }
                int duplicate = Duplicate(int.Parse(Path.GetFileName(entry), CultureInfo.InvariantCulture));
                return duplicate >= 0 ? duplicate : throw new InvalidOperationException("Native descriptor duplication failed.");
            }
            catch (IOException) { } // Unrelated concurrently closed descriptors may disappear during enumeration.
        }
        throw new InvalidOperationException("The private ownership descriptor was not found.");
    }
    [LibraryImport("libc", EntryPoint = "dup", SetLastError = true)]
    private static partial int Duplicate(int descriptor);
    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    internal static partial int Close(int descriptor);
}
