using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Windows;

internal static class WindowsDirectoryPath
{
    private const int _driveRootLength = 3;
    private const int _driveDesignatorLength = 2;
    private const int _minimumStatePathLength = _driveRootLength + 1;
    private const int _maximumPathLength = 32760;
    private const int _dosDeviceBufferBytes = 65536;
    private const int _reservedDevicePrefixLength = 3;
    internal static void Validate(string path)
    {
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw WindowsNative.Failure(ErrorCode.UnsupportedPlatform);
        }
        if (string.IsNullOrWhiteSpace(path) || path.Length < _minimumStatePathLength || path.Length > _maximumPathLength
            || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\'
            || path[_driveRootLength..].Split('\\').Any(component => !ValidFileName(component))
            || !string.Equals(path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
        {
            throw WindowsNative.Failure(ErrorCode.InvalidConfiguration);
        }
        string root = Path.GetPathRoot(path)!;
        DriveInfo drive = new(root);
        if (drive.DriveType != DriveType.Fixed || drive.DriveFormat is not ("NTFS" or "ReFS")) { throw WindowsNative.Failure(); }
        byte[] device = new byte[_dosDeviceBufferBytes];
        uint length = WindowsNative.DosDevice(root[.._driveDesignatorLength], device, (uint)(device.Length / sizeof(char)));
        if (length == 0 || length >= device.Length / sizeof(char)
            || !Encoding.Unicode.GetString(device, 0, checked((int)length * sizeof(char))).StartsWith(@"\Device\HarddiskVolume", StringComparison.Ordinal))
        {
            throw WindowsNative.Failure();
        }
    }
    internal static bool ValidFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.EndsWith('.') || name.EndsWith(' ')
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { return false; }
        string stem = name.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)) { return false; }
        return stem.Length != _reservedDevicePrefixLength + 1 || !(stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
            || stem[_reservedDevicePrefixLength] is not (>= '1' and <= '9' or '\u00b9' or '\u00b2' or '\u00b3');
    }
    internal static List<SafeFileHandle> Open(string path, bool create)
    {
        List<SafeFileHandle> handles = [];
        try
        {
            string current = Path.GetPathRoot(path)!;
            handles.Add(OpenDirectory(current));
            string[] components = path[current.Length..].Split('\\');
            for (int index = 0; index < components.Length; index++)
            {
                current = Path.Combine(current, components[index]);
                if (create && index == components.Length - 1)
                {
                    using WindowsPrivateSecurity security = new(true);
                    if (!WindowsNative.CreateDirectory(WindowsNative.Extended(current), security.Pointer)) { throw WindowsNative.Failure(); }
                }
                handles.Add(OpenDirectory(current));
            }
            WindowsPrivateSecurity.Require(path, true);
            return handles;
        }
        catch { Close(handles); throw; }
    }
    internal static void Close(List<SafeFileHandle> handles)
    {
        for (int index = handles.Count - 1; index >= 0; index--) { handles[index].Dispose(); }
    }
    private static SafeFileHandle OpenDirectory(string path)
    {
        SafeFileHandle handle = WindowsNative.Open(path, WindowsNative.ReadAttributes, WindowsNative.ShareReadWrite, WindowsNative.OpenExisting);
        try
        {
            WindowsFileIdentity.RequireType(handle, true);
            if (!string.Equals(WindowsNative.Extended(path), WindowsNative.Canonical(handle), StringComparison.OrdinalIgnoreCase)) { throw WindowsNative.Failure(); }
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }
}
