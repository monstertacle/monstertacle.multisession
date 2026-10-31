using Microsoft.Win32.SafeHandles;

namespace Monstertacle.Multisession.Windows;

internal sealed record WindowsFileIdentity(ulong Volume, Guid FileId)
{
    private const int _fileIdInformation = 18;
    private const int _attributeTagInformation = 9;
    private const int _standardInformation = 1;
    private const int _fileIdInfoBytes = 24;
    private const int _attributeTagInfoBytes = 8;
    private const int _standardInfoBytes = 24;
    private const int _fileIdOffset = 8;
    private const int _fileIdBytes = 16;
    private const int _linkCountOffset = 16;
    private const int _deletePendingOffset = 20;
    private const int _directoryOffset = 21;
    private const uint _reparsePoint = 0x400;
    internal static WindowsFileIdentity Read(SafeFileHandle handle)
    {
        byte[] value = new byte[_fileIdInfoBytes]; // FILE_ID_INFO: volume serial followed by FILE_ID_128.
        if (!WindowsNative.FileInformation(handle, _fileIdInformation, value, (uint)value.Length)) { throw WindowsNative.Failure(); }
        return new(BitConverter.ToUInt64(value), new Guid(value.AsSpan(_fileIdOffset, _fileIdBytes)));
    }
    internal static void RequireType(SafeFileHandle handle, bool directory)
    {
        byte[] attributes = new byte[_attributeTagInfoBytes]; // FILE_ATTRIBUTE_TAG_INFO.
        byte[] standard = new byte[_standardInfoBytes]; // FILE_STANDARD_INFO, including the two final BOOLEAN fields.
        if (!WindowsNative.FileInformation(handle, _attributeTagInformation, attributes, (uint)attributes.Length)
            || !WindowsNative.FileInformation(handle, _standardInformation, standard, (uint)standard.Length)
            || (BitConverter.ToUInt32(attributes) & _reparsePoint) != 0
            || standard[_deletePendingOffset] != 0 || (standard[_directoryOffset] != 0) != directory
            || (!directory && BitConverter.ToUInt32(standard, _linkCountOffset) != 1))
        {
            throw WindowsNative.Failure();
        }
    }
}
