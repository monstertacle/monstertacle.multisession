namespace Monstertacle.Multisession.ValidationHost;

// Public probe selection and bounded internal worker protocols share one executable.
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        string? mode = args.Length == 0 ? null : args[0];
        return mode switch
        {
            CrashProtocol.WorkerArgument => await CrashWorker.RunAsync(args),
            DiskFullProtocol.WorkerArgument => await DiskFullWorker.RunAsync(args),
#if !WINDOWS
            OwnershipProtocol.WorkerArgument => await LinuxOwnershipWorker.RunAsync(args),
#endif
            _ => await ScenarioRunner.RunAsync(args),
        };
    }
}
