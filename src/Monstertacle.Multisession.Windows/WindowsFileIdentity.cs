using Microsoft.Win32.SafeHandles;

namespace Monstertacle.Multisession.Windows;

internal sealed record WindowsFileIdentity(ulong Volume, Guid FileId)
{
    internal static WindowsFileIdentity Read(SafeFileHandle handle)
    {
        byte[] value = new byte[24]; // FILE_ID_INFO: volume serial followed by FILE_ID_128.
        if (!WindowsNative.FileInformation(handle, 18, value, (uint)value.Length)) { throw WindowsNative.Failure(); }
        return new(BitConverter.ToUInt64(value), new Guid(value.AsSpan(8, 16)));
    }
    internal static void RequireType(SafeFileHandle handle, bool directory)
    {
        byte[] attributes = new byte[8]; // FILE_ATTRIBUTE_TAG_INFO.
        byte[] standard = new byte[24]; // FILE_STANDARD_INFO, including the two final BOOLEAN fields.
        if (!WindowsNative.FileInformation(handle, 9, attributes, (uint)attributes.Length)
            || !WindowsNative.FileInformation(handle, 1, standard, (uint)standard.Length)
            || (BitConverter.ToUInt32(attributes) & 0x400) != 0 // FILE_ATTRIBUTE_REPARSE_POINT.
            || standard[20] != 0 || (standard[21] != 0) != directory
            || (!directory && BitConverter.ToUInt32(standard, 16) != 1))
        {
            throw WindowsNative.Failure();
        }
    }
}
