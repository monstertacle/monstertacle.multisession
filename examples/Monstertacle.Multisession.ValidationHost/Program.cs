namespace Monstertacle.Multisession.ValidationHost;

// Crash infrastructure only; public validation scenarios remain separately planned.
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0) { return 0; }
        return args[0] switch
        {
            CrashProtocol.WorkerArgument => await CrashWorker.RunAsync(args),
            DiskFullProtocol.WorkerArgument => await DiskFullWorker.RunAsync(args),
            _ => 0,
        };
    }
}
