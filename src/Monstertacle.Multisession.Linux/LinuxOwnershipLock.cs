using System.Runtime.InteropServices;

using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Linux;

internal static class LinuxOwnershipLock
{
    private const int _unlock = 8;
    private const int _exclusiveNonBlocking = 2 | 4;
    private const int _alreadyExists = 17;
    private const int _wouldBlock = 11;

    internal static void Release(LinuxFileHandle handle)
    {
        if (handle.IsClosed) { return; }
        try
        {
            // Explicit LOCK_UN also releases copies of the same open file description during fork/exec.
            if (LinuxNative.Flock(LinuxNative.Descriptor(handle), _unlock) != 0) { throw LinuxNative.Failure(); }
        }
        finally { handle.Dispose(); }
    }

    internal static LinuxFileHandle Acquire(LinuxFileHandle directory)
    {
        int parent = LinuxNative.Descriptor(directory);
        int descriptor = LinuxNative.OpenAt(parent, LinuxNative.LockName,
            LinuxNative.FileFlags | LinuxNative.ExclusiveCreate, LinuxNative.FileMode);
        if (descriptor < 0)
        {
            if (Marshal.GetLastPInvokeError() != _alreadyExists) { throw LinuxNative.Failure(); }
            descriptor = LinuxNative.OpenAt(parent, LinuxNative.LockName, LinuxNative.FileFlags, 0);
        }
        if (descriptor < 0) { throw LinuxNative.Failure(); }
        LinuxFileHandle handle = new(descriptor);
        try
        {
            LinuxFileIdentity.Read(handle).RequirePrivate(false);
            if (LinuxNative.Flock(descriptor, _exclusiveNonBlocking) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                throw LinuxNative.Failure(error == _wouldBlock ? ErrorCode.StoreAlreadyOwned : ErrorCode.StorageUnavailable);
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
