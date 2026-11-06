using System.Diagnostics;
using System.Globalization;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>
/// T023 fixture worker (SDK-T023-FIXTURE-WORKER-CRAP-001): the real <see cref="FirstLineFixtureWorker"/> run in process with its
/// own error writer, a private working directory and a short self limit. The child-process path is covered by the integration
/// controls (CrashHarnessTimeoutTests, CrashHarnessStderrMemoryTests).
/// </summary>
public sealed class FirstLineFixtureWorkerTests
{
    private static readonly TimeSpan _neverReached = TimeSpan.FromMinutes(10);

    /// <summary>Argument lists the worker refuses: wrong count, or an unknown variant.</summary>
    public static TheoryData<string[]> RefusedArguments => new()
    {
        Array.Empty<string>(),
        new[] { FirstLineFixtureProtocol.WorkerArgument },
        new[] { FirstLineFixtureProtocol.WorkerArgument, "unknown" },
        new[] { FirstLineFixtureProtocol.WorkerArgument, string.Empty },
        new[] { FirstLineFixtureProtocol.WorkerArgument, FirstLineFixtureProtocol.Silent, "extra" },
    };

    /// <summary>A refused argument list exits 2 at once: no process id is recorded, nothing is written and the limit is not waited for.</summary>
    [Theory]
    [MemberData(nameof(RefusedArguments))]
    public async Task RefusedArgumentsExitTwoWithoutAnyEffect(string[] args)
    {
        string directory = NewDirectory();
        try
        {
            using StringWriter error = new();
            Task<int> run = FirstLineFixtureWorker.RunAsync(args, error, directory, _neverReached);
            Assert.True(run.IsCompleted, "a refused argument list must not wait for the self limit");
            Assert.Equal(2, await run);
            Assert.Empty(error.ToString());
            Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    /// <summary>
    /// The process entry (the worker's own console, current directory and two-minute limit) refuses a wrong argument list at once
    /// with exit 2 and records nothing in the current directory; it only reads those process resources, it changes none.
    /// </summary>
    [Fact]
    public async Task ProcessEntryRefusesWrongArgumentsBeforeTouchingAnyResource()
    {
        string record = Path.Combine(Environment.CurrentDirectory, FirstLineFixtureProtocol.ProcessIdFileName);
        bool recordedBefore = File.Exists(record);
        Task<int> run = FirstLineFixtureWorker.RunAsync([FirstLineFixtureProtocol.WorkerArgument, "unknown"]);
        Assert.True(run.IsCompleted, "a refused argument list must not wait for the two-minute self limit");
        Assert.Equal(2, await run);
        Assert.Equal(recordedBefore, File.Exists(record));
    }

    /// <summary><c>silent</c> records its own process id, writes nothing, does not end before its self limit and then exits 3.</summary>
    [Fact]
    public async Task SilentRecordsItsProcessIdAndEndsOnlyAtItsLimit()
    {
        string directory = NewDirectory();
        TimeSpan limit = TimeSpan.FromMilliseconds(400);
        try
        {
            using StringWriter error = new();
            long began = Stopwatch.GetTimestamp();
            Task<int> run = FirstLineFixtureWorker.RunAsync([FirstLineFixtureProtocol.WorkerArgument, FirstLineFixtureProtocol.Silent], error, directory, limit);
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.False(run.IsCompleted, "the worker ended before its self limit");
            Assert.Equal(3, await run);
            // Lower bound only; the timer may fire up to one clock tick early.
            Assert.True(Stopwatch.GetElapsedTime(began) >= limit - TimeSpan.FromMilliseconds(20), "ended after " + Stopwatch.GetElapsedTime(began).TotalMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms");
            Assert.Empty(error.ToString());
            Assert.Equal(Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                await File.ReadAllTextAsync(Path.Combine(directory, FirstLineFixtureProtocol.ProcessIdFileName)));
        }
        finally { Directory.Delete(directory, true); }
    }

    /// <summary><c>stderr</c> writes exactly its lines of filler, then the marker line, and exits 3.</summary>
    [Fact]
    public async Task StderrWritesItsLinesThenTheMarker()
    {
        string directory = NewDirectory();
        try
        {
            using StringWriter error = new();
            Assert.Equal(3, await FirstLineFixtureWorker.RunAsync([FirstLineFixtureProtocol.WorkerArgument, FirstLineFixtureProtocol.Stderr], error, directory, TimeSpan.Zero));
            string filler = new('e', FirstLineFixtureProtocol.StderrLineLength);
            string expected = string.Concat(Enumerable.Repeat(filler + error.NewLine, FirstLineFixtureProtocol.StderrLines)) + FirstLineFixtureProtocol.Marker + error.NewLine;
            Assert.Equal(expected, error.ToString());
            Assert.True(File.Exists(Path.Combine(directory, FirstLineFixtureProtocol.ProcessIdFileName)));
        }
        finally { Directory.Delete(directory, true); }
    }

    /// <summary>
    /// <c>stderr-unterminated</c> writes the cyclic pattern for exactly its character count, also across the shorter last chunk,
    /// then the marker, with no line terminator anywhere, and exits 3.
    /// </summary>
    [Fact]
    public async Task StderrUnterminatedWritesThePatternAndMarkerWithoutAnyTerminator()
    {
        string directory = NewDirectory();
        try
        {
            using StringWriter error = new();
            Assert.Equal(3, await FirstLineFixtureWorker.RunAsync([FirstLineFixtureProtocol.WorkerArgument, FirstLineFixtureProtocol.StderrUnterminated], error, directory, TimeSpan.Zero));
            string written = error.ToString();
            Assert.Equal(FirstLineFixtureProtocol.UnterminatedChars + FirstLineFixtureProtocol.Marker.Length, written.Length);
            Assert.NotEqual(0, FirstLineFixtureProtocol.UnterminatedChars % FirstLineFixtureProtocol.UnterminatedChunk); // the last chunk is a partial one
            for (int index = 0; index < FirstLineFixtureProtocol.UnterminatedChars; index++)
            {
                if (written[index] != (char)('a' + (index % FirstLineFixtureProtocol.PatternLength)))
                {
                    Assert.Fail("pattern broken at character " + index.ToString(CultureInfo.InvariantCulture));
                }
            }
            Assert.EndsWith(FirstLineFixtureProtocol.Marker, written, StringComparison.Ordinal);
            Assert.DoesNotContain('\n', written);
            Assert.DoesNotContain('\r', written);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static string NewDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "first-line-fixture-worker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
