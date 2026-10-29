
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Linux;

internal static class LinuxDirectoryPath
{
    internal static void Validate(string path)
    {
        if (!OperatingSystem.IsLinux() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64) { throw LinuxNative.Failure(ErrorCode.UnsupportedPlatform); }
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0') || path == "/"
            || !Path.IsPathFullyQualified(path) || path != Path.GetFullPath(path)
            || path.EndsWith('/') || path.Contains("//", StringComparison.Ordinal))
        {
            throw LinuxNative.Failure(ErrorCode.InvalidConfiguration);
        }
    }

    internal static LinuxFileHandle Open(string path, bool create)
    {
        LinuxFileHandle current = LinuxNative.Open(-100, "/", LinuxNative.DirectoryFlags);
        try
        {
            string[] components = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < components.Length; index++)
            {
                int parent = LinuxNative.Descriptor(current);
                if (create && index == components.Length - 1
                    && LinuxNative.MakeDirectoryAt(parent, components[index], LinuxNative.DirectoryMode) != 0)
                {
                    throw LinuxNative.Failure();
                }
                LinuxFileHandle next = LinuxNative.Open(parent, components[index], LinuxNative.DirectoryFlags);
                current.Dispose();
                current = next;
            }
            LinuxFileIdentity.Read(current).RequirePrivate(true);
            LinuxFileIdentity.RequireLocal(current);
            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }
}
