using System.Globalization;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>
/// T022 and SDK-T020-ENTRY-TIMEOUT-FIXTURE-CLEANUP-001: controls of the T020 stop window with owned fixtures only. A stop that ends
/// without entering cleanup and a cleanup entered late must give distinguishable stage lines, and the window's own cleanup must
/// run when the entry wait fails, without replacing that failure.
/// </summary>
public sealed class RuntimeCleanupObservationTests
{
    /// <summary>A worker held by an owned provider: the stop ends at its deadline without entering cleanup; the entry wait fails.</summary>
    [Fact]
    public async Task SilentStopWithoutCleanupIsAttributedAndCleanedUp()
    {
        TaskCompletionSource<ProviderReply> gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ControlledProvider provider = new() { Sending = (_, _, _, _) => new ValueTask<ProviderReply>(gate.Task) }; // ignores cancellation: an owned held worker
        List<string> lines = [];
        BoundedCleanupWindow window = new()
        {
            Provider = provider,
            WithProfile = true,
            Prepare = async runtime => { await SubmitAsync(runtime); await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10)); },
            Release = () => gate.TrySetException(new InvalidOperationException("released by the control")),
        };
        try
        {
            Exception? primary = await Record.ExceptionAsync(() => window.RunAsync(lines.Add));
            Assert.True(primary is TimeoutException, "expected the 5 s entry wait (the site of run 41876640953): " + primary + " lines: " + string.Join(" | ", lines));
            Assert.True(window.CleanupRan, "the window's own cleanup did not run after the entry wait failed");
            Assert.True(window.LeaseDisposed);
            string line = Assert.Single(lines);
            Assert.Contains("cleanup-entry-wait-observed=not-observed", line, StringComparison.Ordinal);
            Assert.Contains(" stop-outcome=faulted:", line, StringComparison.Ordinal);
            Assert.True(Stamp(line, "stop-completed") < 1000, line);
            // Cleanup was entered only by the test's own cleanup (after it began), not by the stop.
            Assert.True(Stamp(line, "cleanup-entered") is not { } entry || entry > Stamp(line, "test-cleanup-began"), line);
        }
        finally
        {
            gate.TrySetException(new InvalidOperationException("released by the control"));
            // Owned safety cleanup: never replaces the control's own result.
            if (window.Runtime is { } runtime) { await Record.ExceptionAsync(async () => await runtime.DisposeAsync()); }
        }
    }

    /// <summary>An acceptance held by an owned hook: the runtime's own cleanup drains it and enters late; the elapsed bound fails.</summary>
    [Fact]
    public async Task LateCleanupEntryIsAttributed()
    {
        TaskCompletionSource accepting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> lines = [];
        BoundedCleanupWindow window = new()
        {
            WithProfile = true,
            CommittingAcceptance = async (_, _) => { accepting.TrySetResult(); await release.Task; },
            Prepare = async runtime =>
            {
                _ = SubmitAsync(runtime); // owned in-flight submit, held by the acceptance hook
                await accepting.Task.WaitAsync(TimeSpan.FromSeconds(10));
                _ = Task.Delay(TimeSpan.FromMilliseconds(1500)).ContinueWith(_ => release.TrySetResult(), TaskScheduler.Default);
            },
            Release = () => release.TrySetResult(),
        };
        try
        {
            Exception? primary = await Record.ExceptionAsync(() => window.RunAsync(lines.Add));
            Assert.True(primary is Xunit.Sdk.TrueException, "expected the original elapsed bound: " + primary + " lines: " + string.Join(" | ", lines));
            Assert.True(window.CleanupRan);
            string line = Assert.Single(lines);
            Assert.True(Stamp(line, "cleanup-entered") is { } entry && entry > 1000 && entry < Stamp(line, "test-cleanup-began"), line);
            Assert.True(Stamp(line, "stop-completed") < 1000, line);
            Assert.Contains(" stop-outcome=faulted:", line, StringComparison.Ordinal);
        }
        finally
        {
            release.TrySetResult();
            // Owned safety cleanup: never replaces the control's own result.
            if (window.Runtime is { } runtime) { await Record.ExceptionAsync(async () => await runtime.DisposeAsync()); }
        }
    }

    private static async Task SubmitAsync(SessionRuntime runtime)
    {
        SessionSnapshot session = await runtime.Sessions.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), CancellationToken.None);
        await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "held", TimeSpan.FromSeconds(30), null, false), CancellationToken.None);
    }

    // The milliseconds of one stage of the line, or null when it was not observed.
    private static double? Stamp(string line, string stage)
    {
        string key = " " + stage + "-ms=";
        int start = line.IndexOf(key, StringComparison.Ordinal);
        if (start < 0) { return null; }
        start += key.Length;
        int end = line.IndexOf(' ', start);
        return double.Parse(end < 0 ? line[start..] : line[start..end], CultureInfo.InvariantCulture);
    }
}
