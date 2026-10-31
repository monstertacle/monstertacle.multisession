namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// The file already belongs to our isolated fixture; restore exact access for cleanup.
internal sealed class ReadOnlyFile : IDisposable
{
    private readonly string _path;
    private readonly FileAttributes _attributes;
    private readonly UnixFileMode? _unixMode;

    internal ReadOnlyFile(string path)
    {
        _path = path;
        _attributes = File.GetAttributes(path);
        if (OperatingSystem.IsWindows()) { File.SetAttributes(path, _attributes | FileAttributes.ReadOnly); }
        else
        {
            _unixMode = File.GetUnixFileMode(path);
            File.SetUnixFileMode(path, UnixFileMode.UserRead);
        }
    }

    internal bool DeniesNewWriteHandle()
    {
        try { using FileStream probe = new(_path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); return false; }
        catch (UnauthorizedAccessException) { return true; }
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows()) { File.SetAttributes(_path, _attributes); }
        else { File.SetUnixFileMode(_path, _unixMode!.Value); }
    }
}
