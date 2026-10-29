using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Windows;

internal static class WindowsOwnershipLock
{
    private const int _fileExists = 80;
    private const int _alreadyExists = 183;
    private const int _sharingViolation = 32;
    private const int _lockViolation = 33;
    internal static SafeFileHandle Acquire(string directory)
    {
        string path = Path.Combine(directory, WindowsNative.LockName);
        using WindowsPrivateSecurity security = new(false);
        SafeFileHandle handle = WindowsNative.CreateFile(WindowsNative.Extended(path), WindowsNative.ReadWrite, WindowsNative.NoSharing,
            security.Pointer, WindowsNative.CreateNew, WindowsNative.OpenFlags, 0);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            if (error is not (_fileExists or _alreadyExists)) { throw Classify(error); }
            handle = WindowsNative.CreateFile(WindowsNative.Extended(path), WindowsNative.ReadWrite, WindowsNative.NoSharing,
                0, WindowsNative.OpenExisting, WindowsNative.OpenFlags, 0);
        }
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw Classify(error);
        }
        try
        {
            WindowsFileIdentity.RequireType(handle, false);
            WindowsPrivateSecurity.Require(path, false);
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }
    private static SessionOperationException Classify(int error)
    {
        Console.Error.WriteLine($"Ownership native-error={error} pid={Environment.ProcessId}");
        return WindowsNative.Failure(error is _sharingViolation or _lockViolation ? ErrorCode.StoreAlreadyOwned : ErrorCode.StorageUnavailable);
    }
}
