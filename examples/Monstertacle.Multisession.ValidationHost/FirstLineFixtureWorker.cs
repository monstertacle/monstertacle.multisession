using System.Globalization;

namespace Monstertacle.Multisession.ValidationHost;

/// <summary>
/// Test fixture worker (T023): never writes a first standard-output line, so a harness waiting for one times out. It records its
/// own process id in its working directory, so a control can show that only this child was ended. <c>silent</c> writes nothing;
/// <c>stderr</c> writes far more than a pipe buffer to standard error, ending with the marker. It ends by itself after
/// <see cref="_selfLimit"/>, so it never outlives a harness that failed to end it.
/// </summary>
internal static class FirstLineFixtureWorker
{
    private static readonly TimeSpan _selfLimit = TimeSpan.FromMinutes(2);

    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 2 || args[1] is not (FirstLineFixtureProtocol.Silent or FirstLineFixtureProtocol.Stderr)) { return 2; }
        await File.WriteAllTextAsync(Path.Combine(Environment.CurrentDirectory, FirstLineFixtureProtocol.ProcessIdFileName),
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        if (args[1] == FirstLineFixtureProtocol.Stderr)
        {
            string filler = new('e', FirstLineFixtureProtocol.StderrLineLength);
            for (int line = 0; line < FirstLineFixtureProtocol.StderrLines; line++) { await Console.Error.WriteLineAsync(filler); }
            await Console.Error.WriteLineAsync(FirstLineFixtureProtocol.Marker);
            await Console.Error.FlushAsync();
        }
        await Task.Delay(_selfLimit);
        return 3;
    }
}
