using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Windows;

internal static class WindowsOwnershipLock
{
    internal static SafeFileHandle Acquire(string directory)
    {
        string path = Path.Combine(directory, WindowsNative.LockName);
        using WindowsPrivateSecurity security = new(false);
        SafeFileHandle handle = WindowsNative.CreateFile(WindowsNative.Extended(path), WindowsNative.ReadWrite, 0,
            security.Pointer, WindowsNative.CreateNew, WindowsNative.OpenFlags, 0);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            if (error is not (80 or 183)) { throw Classify(error); }
            handle = WindowsNative.CreateFile(WindowsNative.Extended(path), WindowsNative.ReadWrite, 0,
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
        => WindowsNative.Failure(error is 32 or 33 ? ErrorCode.StoreAlreadyOwned : ErrorCode.StorageUnavailable);
}
