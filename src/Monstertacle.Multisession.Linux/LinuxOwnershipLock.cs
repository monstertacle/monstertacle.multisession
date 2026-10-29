using System.Runtime.InteropServices;

using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Linux;

internal static class LinuxOwnershipLock
{
    internal static LinuxFileHandle Acquire(LinuxFileHandle directory)
    {
        int parent = LinuxNative.Descriptor(directory);
        int descriptor = LinuxNative.OpenAt(parent, LinuxNative.LockName,
            LinuxNative.FileFlags | LinuxNative.ExclusiveCreate, LinuxNative.FileMode);
        if (descriptor < 0)
        {
            if (Marshal.GetLastPInvokeError() != 17) { throw LinuxNative.Failure(); } // EEXIST only.
            descriptor = LinuxNative.OpenAt(parent, LinuxNative.LockName, LinuxNative.FileFlags, 0);
        }
        if (descriptor < 0) { throw LinuxNative.Failure(); }
        LinuxFileHandle handle = new(descriptor);
        try
        {
            LinuxFileIdentity.Read(handle).RequirePrivate(false);
            if (LinuxNative.Flock(descriptor, 2 | 4) != 0) // LOCK_EX | LOCK_NB.
            {
                int error = Marshal.GetLastPInvokeError();
                throw LinuxNative.Failure(error == 11 ? ErrorCode.StoreAlreadyOwned : ErrorCode.StorageUnavailable);
            }
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }
}
