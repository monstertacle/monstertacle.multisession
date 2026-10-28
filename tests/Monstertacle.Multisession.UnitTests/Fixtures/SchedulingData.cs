using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

internal static class SchedulingData
{
    public static DateTimeOffset Epoch { get; } = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    public static SessionSnapshot Session()
    {
        Guid id = Guid.NewGuid();
        ProviderSessionContext context = new(id, "opaque-model", ExecutionKind.AgentTask,
            new(Path.GetTempPath(), null), new Dictionary<string, string>());
        return new(id, "registered-adapter", "frozen-profile", context, new("test-adapter", 1, "native-session", null))
        {
            Lifecycle = SessionLifecycle.Open,
        };
    }

    public static QueuedRequest Queued(Guid sessionId, long sequence = 1, int acceptedSeconds = 0, string? key = null)
        => new(Guid.NewGuid(), sessionId, sequence, Epoch.AddSeconds(acceptedSeconds), key);

    public static ExecutionReservation Reserved(Guid sessionId, string? key = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), sessionId, key, Epoch);
}
