using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Windows;

internal static partial class WindowsNative
{
    internal const uint ReadAttributes = 0x20080; // READ_CONTROL | FILE_READ_ATTRIBUTES.
    internal const uint ReadWrite = 0xc0000000;
    internal const uint ShareReadWrite = 3; // Deliberately excludes FILE_SHARE_DELETE.
    internal const uint OpenExisting = 3;
    internal const uint CreateNew = 1;
    internal const uint OpenFlags = 0x02000000 | 0x00200000; // BACKUP_SEMANTICS | OPEN_REPARSE_POINT.
    internal const string LockName = ".multisession.lock";

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial SafeFileHandle CreateFile(string path, uint access, uint share, nint attributes, uint disposition, uint flags, nint template);
    [LibraryImport("kernel32.dll", EntryPoint = "CreateDirectoryW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CreateDirectory(string path, nint attributes);
    [LibraryImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FileInformation(SafeFileHandle handle, int kind, [Out] byte[] buffer, uint size);
    [LibraryImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true)]
    internal static partial uint FinalPath(SafeFileHandle handle, [Out] byte[] buffer, uint characters, uint flags);
    [LibraryImport("kernel32.dll", EntryPoint = "QueryDosDeviceW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint DosDevice(string device, [Out] byte[] buffer, uint characters);

    internal static string Extended(string path) => @"\\?\" + path;
    internal static SafeFileHandle Open(string path, uint access, uint share, uint disposition, nint attributes = 0)
    {
        SafeFileHandle handle = CreateFile(Extended(path), access, share, attributes, disposition, OpenFlags, 0);
        if (!handle.IsInvalid) { return handle; }
        handle.Dispose();
        throw Failure();
    }
    internal static string Canonical(SafeFileHandle handle)
    {
        const int maximumCharacters = 32768;
        byte[] buffer = new byte[maximumCharacters * sizeof(char)];
        uint length = FinalPath(handle, buffer, maximumCharacters, 0);
        if (length == 0 || length >= maximumCharacters) { throw Failure(); }
        return Encoding.Unicode.GetString(buffer, 0, checked((int)length * sizeof(char)));
    }
    internal static SessionOperationException Failure(ErrorCode code = ErrorCode.StorageUnavailable) => new(new(code));
}
