namespace Monstertacle.Multisession.ValidationHost;

// T023 fixture worker protocol, shared with the integration tests: a worker that never writes a first standard-output line.
internal static class FirstLineFixtureProtocol
{
    internal const string WorkerArgument = "--first-line-fixture-worker";
    internal const string Silent = "silent";
    internal const string Stderr = "stderr";
    internal const string StderrUnterminated = "stderr-unterminated";
    internal const string Marker = "first-line-fixture-stderr-end";
    internal const string ProcessIdFileName = "first-line-fixture.pid";
    internal const int StderrLines = 2048;
    internal const int StderrLineLength = 63;
    internal const int UnterminatedChars = 8 * 1024 * 1024; // 8 Mi characters without any line terminator
    internal const int UnterminatedChunk = 26 * 2520; // a multiple of the pattern length, so the pattern runs on across chunks
    internal const int PatternLength = 26; // character i of the unterminated stream is 'a' + i % 26
}
