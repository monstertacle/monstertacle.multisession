

namespace Monstertacle.Multisession.Linux;

internal sealed record LinuxFileIdentity(ulong Inode, ulong Device, uint Owner, uint Links, ushort Mode)
{
    private const uint _requiredMask = 0x1 | 0x2 | 0x4 | 0x8 | 0x100;
    internal static LinuxFileIdentity Read(LinuxFileHandle handle)
    {
        byte[] value = new byte[256]; // Linux statx fixed ABI, independent of libc's stat layout.
        if (LinuxNative.Stat(LinuxNative.Descriptor(handle), "", 0x1000, _requiredMask, value) != 0
            || (BitConverter.ToUInt32(value) & _requiredMask) != _requiredMask)
        {
            throw LinuxNative.Failure();
        }
        return new(BitConverter.ToUInt64(value, 32), BitConverter.ToUInt64(value, 136),
            BitConverter.ToUInt32(value, 20), BitConverter.ToUInt32(value, 16), BitConverter.ToUInt16(value, 28));
    }

    internal void RequirePrivate(bool directory)
    {
        int expected = directory ? 0x4000 | (int)LinuxNative.DirectoryMode : 0x8000 | (int)LinuxNative.FileMode;
        if (Owner != LinuxNative.EffectiveUser() || Mode != expected || (!directory && Links != 1))
        {
            throw LinuxNative.Failure();
        }
    }

    internal bool SameObject(LinuxFileIdentity other) => Inode == other.Inode && Device == other.Device;

    internal static void RequireLocal(LinuxFileHandle directory)
    {
        // fstatfs is at most 120 bytes on supported Linux x64; reserve extra space for ABI padding.
        byte[] value = new byte[256];
        if (LinuxNative.FileSystem(LinuxNative.Descriptor(directory), value) != 0) { throw LinuxNative.Failure(); }
        ulong type = BitConverter.ToUInt64(value);
        if (type is not (0xef53 or 0x58465342 or 0x9123683e or 0x01021994 or 0x794c7630 or 0x2fc12fc1))
        {
            throw LinuxNative.Failure();
        }
    }
}
