using System.Diagnostics;
using System.Globalization;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

/// <summary>
/// One read-only lookup of a failed candidate with its own finite token and an outer bound. The result is what the store showed
/// at that moment: absence is not proof of non-acceptance, and nothing is resubmitted.
/// </summary>
internal static class CandidateLookup
{
    private static readonly TimeSpan _outerMargin = TimeSpan.FromSeconds(1);

    internal static async Task<string> ObserveAsync(Func<Guid, CancellationToken, ValueTask<RequestSnapshot?>> lookup, Guid candidate, TimeSpan bound,
        LookupSeams? seams = null)
    {
        LookupSeams use = seams ?? LookupSeams.Default;
        long began = Stopwatch.GetTimestamp();
        using CancellationTokenSource deadline = new(bound);
        Task<RequestSnapshot?>? pending = null;
        string result;
        try
        {
            pending = lookup(candidate, deadline.Token).AsTask();
            RequestSnapshot? found = await use.Wait(pending, bound + _outerMargin);
            result = found is null ? "absent-at-observation" : found.State + "/" + found.Certainty;
        }
        catch (TimeoutException timeout) when (pending is not null)
        {
            // The lookup may still run, or may have faulted after the outer wait gave up; its fault is observed either way.
            result = pending.IsFaulted && pending.Exception!.InnerExceptions.Contains(timeout) ? "error:" + timeout.GetType().FullName : "timeout";
            use.Observe(pending);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested) { result = "timeout"; }
        catch (Exception failure) { result = "error:" + failure.GetType().FullName; }
        return result + " lookup-ms=" + Stopwatch.GetElapsedTime(began).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);
    }
}

/// <summary>The outer wait and the late-fault observer of a lookup; tests replace them to force an exact interleaving.</summary>
internal sealed record LookupSeams(Func<Task<RequestSnapshot?>, TimeSpan, Task<RequestSnapshot?>> Wait, Action<Task> Observe)
{
    internal static LookupSeams Default { get; } = new(static (task, timeout) => task.WaitAsync(timeout), TaskObservation.ObserveLateFault);
}
