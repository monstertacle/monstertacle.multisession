namespace Monstertacle.Multisession.ValidationHost;

// Crash infrastructure only; public validation scenarios remain separately planned.
internal static class Program
{
    private static async Task<int> Main(string[] args)
        => args.Length > 0 && args[0] == CrashProtocol.WorkerArgument ? await CrashWorker.RunAsync(args) : 0;
}
