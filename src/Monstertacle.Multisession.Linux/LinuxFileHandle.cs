using Microsoft.Win32.SafeHandles;

namespace Monstertacle.Multisession.Linux;

// Unix descriptor zero is valid; SafeFileHandle also treats zero as invalid.
internal sealed class LinuxFileHandle : SafeHandleMinusOneIsInvalid
{
    internal LinuxFileHandle(int descriptor) : base(true) => SetHandle(descriptor);
    protected override bool ReleaseHandle() => LinuxNative.Close(handle.ToInt32()) == 0;
}
