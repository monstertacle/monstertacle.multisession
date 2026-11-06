using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>
/// T023: a crash worker that writes no first line within the harness limit gives an attributed timeout: the limit, the elapsed
/// time, that no worker step is known, and a bounded tail of its standard error. Only the harness's own child is ended. The owned
/// fixture workers run with a short control limit; the production limit is unchanged.
/// </summary>
public sealed class CrashHarnessTimeoutTests
{
    private static readonly TimeSpan _controlLimit = TimeSpan.FromSeconds(2);

    /// <summary>A worker that writes nothing: the timeout says no step is known and no standard error was written.</summary>
    [Fact]
    public async Task SilentWorkerTimeoutIsAttributed()
    {
        string message = await TimeOutAsync(FirstLineFixtureProtocol.Silent);
        Assert.Contains("standard error: none", message, StringComparison.Ordinal);
    }

    /// <summary>A worker that fills its standard-error pipe: it is not blocked, and the timeout carries the bounded tail.</summary>
    [Fact]
    public async Task StderrOnlyWorkerTimeoutCarriesItsBoundedTail()
    {
        string message = await TimeOutAsync(FirstLineFixtureProtocol.Stderr);
        Assert.Contains(FirstLineFixtureProtocol.Marker, message, StringComparison.Ordinal); // written after more than a pipe buffer
        Assert.Contains("standard error: " + ((FirstLineFixtureProtocol.StderrLines * (FirstLineFixtureProtocol.StderrLineLength + Environment.NewLine.Length)) + FirstLineFixtureProtocol.Marker.Length + Environment.NewLine.Length).ToString(CultureInfo.InvariantCulture) + " chars", message, StringComparison.Ordinal);
        Assert.True(message.Length < 8192, "the standard-error report is not bounded: " + message.Length.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Ending the child fails after the timeout: the attributed timeout stays first, the failure follows it.</summary>
    [Fact]
    public async Task FailureToEndTheChildIsReportedAfterTheTimeout()
    {
        await using PrivateStoreDirectory directory = new();
        using Process child = new() { StartInfo = CrashHarness.WorkerStart(directory.StateDirectory, FirstLineFixtureProtocol.WorkerArgument, FirstLineFixtureProtocol.Silent) };
        FirstLineWait firstLine = new(child, _controlLimit);
        InvalidOperationException owned = new("owned failure of the control");
        Assert.True(child.Start());
        try
        {
            Exception? thrown = await Record.ExceptionAsync(() => firstLine.ReadAsync(async () =>
            {
                child.Kill(true); // the control's own child only
                await child.WaitForExitAsync();
                throw owned;
            }));
            AggregateException both = Assert.IsType<AggregateException>(thrown);
            TimeoutException timeout = Assert.IsType<TimeoutException>(both.InnerExceptions[0]);
            Assert.Contains("could not be ended (next exception)", timeout.Message, StringComparison.Ordinal);
            Assert.Same(owned, both.InnerExceptions[1]);
        }
        finally
        {
            if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(); }
        }
    }

    /// <summary>
    /// SDK-SQ-001: 8 Mi characters of standard error without any line terminator. The reader keeps draining from the start, the
    /// tail stores at most its capacity at any time, keeps exactly the last characters and counts every one; the allocation
    /// during the wait stays far below the stream (process-wide allocated bytes, a test-only measure with a stated margin).
    /// </summary>
    [Fact]
    public async Task UnterminatedStderrIsReadInBoundedChunks()
    {
        await using PrivateStoreDirectory directory = new();
        using Process child = new() { StartInfo = CrashHarness.WorkerStart(directory.StateDirectory, FirstLineFixtureProtocol.WorkerArgument, FirstLineFixtureProtocol.StderrUnterminated) };
        FirstLineWait firstLine = new(child, _controlLimit);
        Assert.True(child.Start());
        try
        {
            long before = GC.GetTotalAllocatedBytes(precise: true);
            Exception? thrown = await Record.ExceptionAsync(() => firstLine.ReadAsync(async () =>
            {
                child.Kill(true); // the control's own child only
                await child.WaitForExitAsync();
            }));
            long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
            TimeoutException timeout = Assert.IsType<TimeoutException>(thrown);
            string measured = "tail peak " + firstLine.StandardError.PeakStored.ToString(CultureInfo.InvariantCulture) + " chars, allocated "
                + allocated.ToString(CultureInfo.InvariantCulture) + " bytes";
            Assert.True(firstLine.StandardError.PeakStored <= 2048, "tail storage grew: " + measured);
            // The stream is 16 MiB as UTF-16; a whole-line reader allocates at least that. The bound is half the stream's characters in bytes.
            Assert.True(allocated < FirstLineFixtureProtocol.UnterminatedChars, "allocation grew with the stream: " + measured);
            long total = FirstLineFixtureProtocol.UnterminatedChars + FirstLineFixtureProtocol.Marker.Length;
            Assert.Contains("standard error: " + total.ToString(CultureInfo.InvariantCulture) + " chars, last 2048: ", timeout.Message, StringComparison.Ordinal);
            Assert.EndsWith(new string('u', 2048 - FirstLineFixtureProtocol.Marker.Length) + FirstLineFixtureProtocol.Marker, timeout.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(); }
        }
    }

    /// <summary>SDK-SQ-002: a private path with no executable never starts; its start failure is thrown as is, not replaced by cleanup.</summary>
    [Fact]
    public async Task NeverStartedWorkerKeepsItsStartFailure()
    {
        await using PrivateStoreDirectory directory = new();
        ProcessStartInfo start = CrashHarness.WorkerStart(directory.StateDirectory, FirstLineFixtureProtocol.WorkerArgument, FirstLineFixtureProtocol.Silent);
        start.FileName = Path.Combine(directory.StateDirectory, "no-such-executable");
        Exception? thrown = await Record.ExceptionAsync(() => CrashHarness.StartWorkerAsync(start, CrashProtocol.RecoveryPoint, _controlLimit));
        Assert.IsType<Win32Exception>(thrown); // the original start failure, not an AggregateException with a cleanup failure
        Assert.False(File.Exists(Path.Combine(directory.StateDirectory, FirstLineFixtureProtocol.ProcessIdFileName))); // no child ran
    }

    /// <summary>
    /// SDK-SQ-002: ending the harness's own started child fails (its own store file is unreadable after the child was killed); the
    /// failure is reported and the captured Process is disposed all the same (observed: it no longer has an associated process).
    /// Linux only: the failure uses a Unix file mode.
    /// </summary>
    [Fact]
    public async Task ProcessIsDisposedWhenEndingTheChildFails()
    {
        if (OperatingSystem.IsWindows()) { return; } // the owned failure needs a Unix file mode; recorded as a limit
        await using PrivateStoreDirectory directory = new();
        string database = Path.Combine(directory.StateDirectory, "multisession.db");
        await File.WriteAllBytesAsync(database, []);
        File.SetUnixFileMode(database, UnixFileMode.None);
        Process child = new() { StartInfo = CrashHarness.WorkerStart(directory.StateDirectory, FirstLineFixtureProtocol.WorkerArgument, FirstLineFixtureProtocol.Silent) };
        Assert.True(child.Start());
        int processId = child.Id;
        try
        {
            Exception? thrown = await Record.ExceptionAsync(async () => await new CrashHarness(child).DisposeAsync());
            Assert.IsType<UnauthorizedAccessException>(thrown);
            Assert.False(Running(processId), "the own child is still running");
            // A disposed Process no longer has an associated process; an undisposed one answers that its killed child exited.
            Assert.Throws<InvalidOperationException>(() => child.HasExited);
        }
        finally
        {
            File.SetUnixFileMode(database, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            // Owned safety cleanup without masking the result: only this test's own captured child.
            await Record.ExceptionAsync(async () => { if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(); } });
            child.Dispose();
        }
    }

    private static async Task<string> TimeOutAsync(string variant)
    {
        await using PrivateStoreDirectory directory = new();
        ProcessStartInfo start = CrashHarness.WorkerStart(directory.StateDirectory, FirstLineFixtureProtocol.WorkerArgument, variant);
        long began = Stopwatch.GetTimestamp();
        Exception? thrown = await Record.ExceptionAsync(() => CrashHarness.StartWorkerAsync(start, CrashProtocol.RecoveryPoint, _controlLimit));
        TimeSpan elapsed = Stopwatch.GetElapsedTime(began);
        TimeoutException timeout = Assert.IsType<TimeoutException>(thrown); // the primary type is kept
        Assert.IsType<TimeoutException>(timeout.InnerException);
        Assert.Contains("no first line within 2.0 s", timeout.Message, StringComparison.Ordinal);
        Assert.Contains("no worker step is known", timeout.Message, StringComparison.Ordinal);
        double reported = Seconds(timeout.Message, "elapsed ");
        Assert.True(reported >= _controlLimit.TotalSeconds && reported <= elapsed.TotalSeconds + 0.1, timeout.Message);

        // Only the harness's own child, identified by the process id it recorded, was ended.
        int processId = int.Parse(await File.ReadAllTextAsync(Path.Combine(directory.StateDirectory, FirstLineFixtureProtocol.ProcessIdFileName)), CultureInfo.InvariantCulture);
        Assert.Contains("own worker process " + processId.ToString(CultureInfo.InvariantCulture) + " ended", timeout.Message, StringComparison.Ordinal);
        Assert.False(Running(processId), "the fixture worker is still running");
        return timeout.Message;
    }

    private static bool Running(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException) { return false; }
    }

    private static double Seconds(string message, string key)
    {
        int start = message.IndexOf(key, StringComparison.Ordinal);
        Assert.True(start >= 0, message);
        start += key.Length;
        int end = message.IndexOf(' ', start);
        return double.Parse(message[start..end], CultureInfo.InvariantCulture);
    }
}
