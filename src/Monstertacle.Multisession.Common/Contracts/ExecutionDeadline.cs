namespace Monstertacle.Multisession.Contracts;

// One shared definition of a positive finite delay supported by the runtime's timer APIs.
internal static class ExecutionDeadline
{
    public static bool IsValid(TimeSpan deadline)
        => deadline > TimeSpan.Zero && deadline <= RuntimeDefaults.MaximumTimerTimeout;
}
