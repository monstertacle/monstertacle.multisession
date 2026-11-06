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
        Assert.Contains("standard error: " + ((FirstLineFixtureProtocol.StderrLines * (FirstLineFixtureProtocol.StderrLineLength + 1)) + FirstLineFixtureProtocol.Marker.Length + 1).ToString(CultureInfo.InvariantCulture) + " chars", message, StringComparison.Ordinal);
        Assert.True(message.Length < 8192, "the standard-error report is not bounded: " + message.Length.ToString(CultureInfo.InvariantCulture));
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
