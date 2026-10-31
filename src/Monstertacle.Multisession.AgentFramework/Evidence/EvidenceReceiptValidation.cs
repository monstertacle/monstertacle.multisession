namespace Monstertacle.Multisession.AgentFramework.Evidence;

internal static class EvidenceReceiptValidation
{
    internal static async ValueTask<bool> InvokeAsync(Func<EvidenceNode, EvidenceReceipt, CancellationToken, ValueTask<bool>> validate,
        EvidenceNode node, EvidenceReceipt receipt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Isolate synchronous callback prefixes as well as async waits. The caller deadline
        // cannot stop host code physically, but a late result must never publish acceptance.
        Task<bool> validation = Task.Run(() => validate(node, receipt, cancellationToken).AsTask(), cancellationToken);
        _ = validation.ContinueWith(static completed => { _ = completed.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        bool valid = await validation.WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return valid;
    }
}
