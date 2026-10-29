using System.Runtime.InteropServices;

using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Linux;

internal static partial class LinuxNative
{
    internal const int DirectoryFlags = 0x10000 | 0x20000 | 0x80000;
    internal const int FileFlags = 2 | 0x20000 | 0x80000 | 0x800;
    internal const int ExclusiveCreate = 0x40 | 0x80;
    internal const uint DirectoryMode = 448;
    internal const uint FileMode = 384;
    internal const string LockName = ".multisession.lock";

    [LibraryImport("libc", EntryPoint = "openat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int OpenAt(int directory, string path, int flags, uint mode);
    [LibraryImport("libc", EntryPoint = "mkdirat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int MakeDirectoryAt(int directory, string path, uint mode);
    [LibraryImport("libc", EntryPoint = "flock", SetLastError = true)]
    internal static partial int Flock(int descriptor, int operation);
    [LibraryImport("libc", EntryPoint = "geteuid")]
    internal static partial uint EffectiveUser();
    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    internal static partial int Close(int descriptor);
    [LibraryImport("libc", EntryPoint = "statx", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Stat(int descriptor, string path, int flags, uint mask, [Out] byte[] result);
    [LibraryImport("libc", EntryPoint = "fstatfs", SetLastError = true)]
    internal static partial int FileSystem(int descriptor, [Out] byte[] result);

    internal static LinuxFileHandle Open(int directory, string name, int flags, uint mode = 0)
    {
        int descriptor = OpenAt(directory, name, flags, mode);
        if (descriptor < 0) { throw Failure(); }
        return new LinuxFileHandle(descriptor);
    }

    internal static int Descriptor(LinuxFileHandle handle) => handle.DangerousGetHandle().ToInt32();
    internal static SessionOperationException Failure(ErrorCode code = ErrorCode.StorageUnavailable) => new(new(code));
}
