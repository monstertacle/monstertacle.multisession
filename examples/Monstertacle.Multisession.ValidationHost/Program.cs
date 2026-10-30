namespace Monstertacle.Multisession.ValidationHost;

// Public probe selection and bounded internal worker protocols share one executable.
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        string? mode = args.Length == 0 ? null : args[0];
        return mode switch
        {
            StoreSchedulerProbe.Argument when args.Length == 2 => await StoreSchedulerProbe.RunAsync(args[1]),
            CrashProtocol.WorkerArgument => await CrashWorker.RunAsync(args),
            CrashProtocol.NativeWorkerArgument => await CrashWorker.RunAsync(args, native: true),
            DiskFullProtocol.WorkerArgument => await DiskFullWorker.RunAsync(args),
            OwnershipProtocol.WorkerArgument => await NativeOwnershipWorker.RunAsync(args),
            _ => await ScenarioRunner.RunAsync(args),
        };
    }
}
