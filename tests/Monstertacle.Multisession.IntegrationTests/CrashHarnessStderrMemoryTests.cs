using System.Diagnostics;
using System.Globalization;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>
/// T023, SDK-SQ-001: the first-line wait reads a child's standard error in bounded chunks into a bounded tail. Its control reads the
/// process-wide allocated-bytes counter, so it runs without competing test collections.
/// </summary>
[Collection("Process-wide allocation measurement")]
public sealed class CrashHarnessStderrMemoryTests
{
    private const int _tail = 2048;
    private static readonly TimeSpan _controlLimit = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 8 Mi characters of a cyclic pattern without any line terminator, then the marker. The reader keeps draining from the start;
    /// the tail never stores more than its capacity, keeps exactly the last characters and counts every one; the allocation during
    /// the wait stays far below the stream (a whole-line reader allocates at least the stream's 16 MiB as UTF-16; measured
    /// 71 MB on 76b0e19, about 4.5 MB with bounded chunks).
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
            Assert.True(firstLine.StandardError.PeakStored <= _tail, "tail storage grew: " + measured);
            Assert.True(allocated < FirstLineFixtureProtocol.UnterminatedChars, "allocation grew with the stream: " + measured);
            long total = FirstLineFixtureProtocol.UnterminatedChars + FirstLineFixtureProtocol.Marker.Length;
            Assert.Contains("standard error: " + total.ToString(CultureInfo.InvariantCulture) + " chars, last 2048: ", timeout.Message, StringComparison.Ordinal);
            Assert.EndsWith(ExpectedTail(), timeout.Message, StringComparison.Ordinal);
        }
        finally
        {
            await Record.ExceptionAsync(async () => { if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(); } });
        }
    }

    // The last 2048 characters the worker wrote: the end of the pattern (character i is 'a' + i % 26), then the marker.
    private static string ExpectedTail()
    {
        int pattern = _tail - FirstLineFixtureProtocol.Marker.Length;
        char[] tail = new char[pattern];
        for (int index = 0; index < pattern; index++)
        {
            tail[index] = (char)('a' + ((FirstLineFixtureProtocol.UnterminatedChars - pattern + index) % FirstLineFixtureProtocol.PatternLength));
        }
        return new string(tail) + FirstLineFixtureProtocol.Marker;
    }
}
