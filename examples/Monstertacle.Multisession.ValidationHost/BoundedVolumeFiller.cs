namespace Monstertacle.Multisession.ValidationHost;

// Only an explicitly small fresh volume is filled, with an independent hard byte ceiling.
internal static class BoundedVolumeFiller
{
    internal const string FileName = "disk-full-filler.dat";
    private const int _bufferBytes = 64 * 1024;

    internal static async Task<(int Code, long Bytes)> FillAsync(string directory, CancellationToken token)
    {
        long capacity = new DriveInfo(directory).TotalSize;
        if (capacity <= 0 || capacity > DiskFullProtocol.MaximumVolumeBytes) { throw new InvalidOperationException("Probe volume capacity is not bounded."); }
        string path = Path.Combine(directory, FileName);
        int code = 0;
        await using (FileStream filler = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, _bufferBytes, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            byte[] buffer = new byte[_bufferBytes];
            try
            {
                for (long written = 0; written < DiskFullProtocol.MaximumVolumeBytes; written += buffer.Length)
                {
                    await filler.WriteAsync(buffer, token);
                }
                filler.Flush(true);
            }
            catch (IOException failure) when (DiskFullProtocol.IsFullCode(failure.HResult & ushort.MaxValue))
            {
                code = failure.HResult & ushort.MaxValue;
            }
        }
        if (code == 0) { throw new InvalidOperationException("The bounded probe volume did not become full."); }
        return (code, new FileInfo(path).Length);
    }
}
