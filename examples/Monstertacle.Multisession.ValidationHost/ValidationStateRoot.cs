using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal static class ValidationStateRoot
{
    private const string _markerName = ".multisession-validation-root";
    private const string _markerValue = "Monstertacle.Multisession.ValidationRoot/v1";

    internal static async Task<ValidationRunState> CreateRunAsync(string requestedRoot, CancellationToken token)
    {
        string root = Path.TrimEndingDirectorySeparator(requestedRoot);
        if (File.Exists(root)) { throw new ValidationProbeException("InvalidStateRoot"); }
        bool exists = Directory.Exists(root);
        bool marked = exists && HasMarker(root);
        if (exists && !marked && Directory.EnumerateFileSystemEntries(root).Any())
        {
            throw new ValidationProbeException("UnownedStateRoot");
        }
        await using NativeLease rootLease = exists ? NativeLease.OpenExisting(root) : NativeLease.CreateNew(root);
        if (marked) { await rootLease.PrepareFileAsync(_markerName, StateFileMode.OpenExisting, token); }
        else
        {
            await rootLease.PrepareFileAsync(_markerName, StateFileMode.CreateNew, token);
            await File.WriteAllTextAsync(Path.Combine(root, _markerName), _markerValue, token);
        }
        Guid id = Guid.NewGuid();
        NativeLease lease = NativeLease.CreateNew(Path.Combine(root, "run-" + id.ToString("N")));
        return new(id, lease);
    }

    private static bool HasMarker(string root)
    {
        string path = Path.Combine(root, _markerName);
        FileInfo marker = new(path);
        return marker.Exists && marker.Length == _markerValue.Length
            && string.Equals(File.ReadAllText(path), _markerValue, StringComparison.Ordinal);
    }
}
