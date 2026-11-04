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

    internal static async Task<string> ObserveAsync(Func<Guid, CancellationToken, ValueTask<RequestSnapshot?>> lookup, Guid candidate, TimeSpan bound)
    {
        long began = Stopwatch.GetTimestamp();
        using CancellationTokenSource deadline = new(bound);
        Task<RequestSnapshot?>? pending = null;
        string result;
        try
        {
            pending = lookup(candidate, deadline.Token).AsTask();
            RequestSnapshot? found = await pending.WaitAsync(bound + _outerMargin);
            result = found is null ? "absent-at-observation" : found.State + "/" + found.Certainty;
        }
        catch (TimeoutException) when (pending is { IsCompleted: false })
        {
            result = "timeout";
            TaskObservation.ObserveLateFault(pending);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested) { result = "timeout"; }
        catch (Exception failure) { result = "error:" + failure.GetType().FullName; }
        return result + " lookup-ms=" + Stopwatch.GetElapsedTime(began).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);
    }
}
