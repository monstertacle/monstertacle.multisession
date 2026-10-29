using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal static class DiskFullProtocol
{
    internal const string WorkerArgument = "--disk-full-worker";
    internal const string SeedFile = "disk-full-seed.db";
    internal const string ManifestFile = "disk-full.json";
    internal const string MarkerFile = ".disk-full-marker";
    internal const string VolumeDirectory = "volume";
    internal const long MaximumVolumeBytes = 128L * 1024 * 1024;
    internal const int NoSpaceCode = 28;
    internal const int DiskFullCode = 112;
    internal const int HandleDiskFullCode = 39;
    internal static bool IsFullCode(int code) => OperatingSystem.IsWindows()
        ? code is DiskFullCode or HandleDiskFullCode : OperatingSystem.IsLinux() && code == NoSpaceCode;
}

internal sealed record DiskFullManifest(Guid TrustScopeId, Guid SessionId, Guid CompletedRequestId, string StateDirectory);

internal sealed record DiskFullReport(string Nonce, int ProcessId, string Stage)
{
    public DateTimeOffset MeasuredUtc { get; init; }
    public int FullCode { get; init; }
    public long FilledBytes { get; init; }
    public PublicError? Failure { get; init; }
    public bool Verified { get; init; }
}
