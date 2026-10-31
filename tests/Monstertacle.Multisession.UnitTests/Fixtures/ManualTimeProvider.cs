using Microsoft.Extensions.Time.Testing;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Test-only epoch convention; the standard fake drives both timestamps and BCL timers.
internal sealed class ManualTimeProvider() : FakeTimeProvider(DateTimeOffset.UnixEpoch);
