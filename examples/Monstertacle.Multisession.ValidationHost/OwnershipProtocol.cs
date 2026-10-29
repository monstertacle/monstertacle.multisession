using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal static class OwnershipProtocol
{
    internal const string WorkerArgument = "--ownership-worker";
    internal const string MarkerFileName = ".ownership-probe-token";
    internal const string DirectoryPrefix = "multisession-native-";
}

internal sealed record OwnershipSignal(int ProcessId, bool Acquired, ErrorCode? Failure);
