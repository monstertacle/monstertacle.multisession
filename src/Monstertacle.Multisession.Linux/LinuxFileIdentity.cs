

namespace Monstertacle.Multisession.Linux;

internal sealed record LinuxFileIdentity(ulong Inode, ulong Device, uint Owner, uint Links, ushort Mode)
{
    private const uint _requiredMask = 0x1 | 0x2 | 0x4 | 0x8 | 0x100;
    private const int _emptyPath = 0x1000;
    private const int _statxBufferBytes = 256;
    private const int _statfsBufferBytes = 256;
    private const int _inodeOffset = 32;
    private const int _deviceOffset = 136;
    private const int _ownerOffset = 20;
    private const int _linkCountOffset = 16;
    private const int _modeOffset = 28;
    private const ulong _ext4 = 0xef53;
    private const ulong _xfs = 0x58465342;
    private const ulong _btrfs = 0x9123683e;
    private const ulong _tmpfs = 0x01021994;
    private const ulong _overlay = 0x794c7630;
    private const ulong _zfs = 0x2fc12fc1;
    internal static LinuxFileIdentity Read(LinuxFileHandle handle)
    {
        byte[] value = new byte[_statxBufferBytes]; // Linux statx fixed ABI, independent of libc's stat layout.
        if (LinuxNative.Stat(LinuxNative.Descriptor(handle), "", _emptyPath, _requiredMask, value) != 0
            || (BitConverter.ToUInt32(value) & _requiredMask) != _requiredMask)
        {
            throw LinuxNative.Failure();
        }
        return new(BitConverter.ToUInt64(value, _inodeOffset), BitConverter.ToUInt64(value, _deviceOffset),
            BitConverter.ToUInt32(value, _ownerOffset), BitConverter.ToUInt32(value, _linkCountOffset), BitConverter.ToUInt16(value, _modeOffset));
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
        byte[] value = new byte[_statfsBufferBytes];
        if (LinuxNative.FileSystem(LinuxNative.Descriptor(directory), value) != 0) { throw LinuxNative.Failure(); }
        ulong type = BitConverter.ToUInt64(value);
        if (type is not (_ext4 or _xfs or _btrfs or _tmpfs or _overlay or _zfs))
        {
            throw LinuxNative.Failure();
        }
    }
}
