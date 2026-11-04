namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

/// <summary>Observes the fault of a diagnostic task that was not joined, so no failure is lost unobserved.</summary>
internal static class TaskObservation
{
    internal static void ObserveLateFault(Task task)
        => task.ContinueWith(static completed => _ = completed.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
