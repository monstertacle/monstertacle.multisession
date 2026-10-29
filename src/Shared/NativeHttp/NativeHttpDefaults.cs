namespace Monstertacle.Multisession.NativeHttp;

internal static class NativeHttpDefaults
{
    internal const int MaximumResponseBytes = 32 * 1024 * 1024;
    internal static readonly TimeSpan HeaderTimeout = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
}
