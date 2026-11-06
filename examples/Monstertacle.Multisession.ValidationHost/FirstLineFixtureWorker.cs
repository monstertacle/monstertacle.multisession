using System.Globalization;

namespace Monstertacle.Multisession.ValidationHost;

/// <summary>
/// Test fixture worker (T023): never writes a first standard-output line, so a harness waiting for one times out. It records its
/// own process id in its working directory, so a control can show that only this child was ended. <c>silent</c> writes nothing;
/// <c>stderr</c> writes far more than a pipe buffer to standard error, ending with the marker. <c>stderr-unterminated</c> writes 8 Mi characters of a cyclic pattern and the marker without any line terminator. It ends by itself after
/// <see cref="_selfLimit"/>, so it never outlives a harness that failed to end it.
/// </summary>
internal static class FirstLineFixtureWorker
{
    private static readonly TimeSpan _selfLimit = TimeSpan.FromMinutes(2);

    // The worker process: its own standard error, working directory and self limit.
    internal static Task<int> RunAsync(string[] args) => RunAsync(args, Console.Error, Environment.CurrentDirectory, _selfLimit);

    // SDK-T023-FIXTURE-WORKER-CRAP-001: the same behavior with its process resources passed in, so a test can observe the output,
    // the recorded process id and the self limit in process without redirecting the global console or waiting two minutes.
    internal static async Task<int> RunAsync(string[] args, TextWriter error, string directory, TimeSpan selfLimit)
    {
        if (args.Length != 2 || args[1] is not (FirstLineFixtureProtocol.Silent or FirstLineFixtureProtocol.Stderr or FirstLineFixtureProtocol.StderrUnterminated)) { return 2; }
        await File.WriteAllTextAsync(Path.Combine(directory, FirstLineFixtureProtocol.ProcessIdFileName),
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        if (args[1] == FirstLineFixtureProtocol.Stderr)
        {
            string filler = new('e', FirstLineFixtureProtocol.StderrLineLength);
            for (int line = 0; line < FirstLineFixtureProtocol.StderrLines; line++) { await error.WriteLineAsync(filler); }
            await error.WriteLineAsync(FirstLineFixtureProtocol.Marker);
            await error.FlushAsync();
        }
        if (args[1] == FirstLineFixtureProtocol.StderrUnterminated)
        {
            string chunk = string.Create(FirstLineFixtureProtocol.UnterminatedChunk, 0, (span, _) =>
            {
                for (int index = 0; index < span.Length; index++) { span[index] = (char)('a' + (index % FirstLineFixtureProtocol.PatternLength)); }
            });
            for (int written = 0; written < FirstLineFixtureProtocol.UnterminatedChars; written += chunk.Length)
            {
                await error.WriteAsync(chunk.AsMemory(0, Math.Min(chunk.Length, FirstLineFixtureProtocol.UnterminatedChars - written)));
            }
            await error.WriteAsync(FirstLineFixtureProtocol.Marker); // still no line terminator
            await error.FlushAsync();
        }
        await Task.Delay(selfLimit);
        return 3;
    }
}
