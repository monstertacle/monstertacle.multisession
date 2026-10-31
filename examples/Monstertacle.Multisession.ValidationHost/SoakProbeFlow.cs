using System.Security.Cryptography;
using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

internal static class SoakProbeFlow
{
    internal static async Task ExecuteAsync(ValidationRunState state)
    {
        string assemblyHash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(typeof(SoakProbeFlow).Assembly.Location)));
        TimeProvider clock = TimeProvider.System;
        ProbeProvider provider = new("soak-contract", new(), (call, binding, _) => ValueTask.FromResult(ProbeReplies.Success(call, binding)));
        await using ProbeHost host = await ProbeHost.StartAsync(state, [new("soak", "soak-contract", 1, provider)]);
        SoakSchedule schedule = new(clock);
        DateTimeOffset startedUtc = clock.GetUtcNow();
        SessionSnapshot session = await host.CreateAsync("soak");
        List<Guid> requests = [];
        List<SoakAcceptance> acceptances = [];
        while (schedule.NeedsRequest)
        {
            await Task.Delay(schedule.UntilNext, clock, CancellationToken.None);
            RequestReceipt receipt = await host.SubmitAsync(session.SessionId, "soak-" + schedule.AcceptedCount);
            schedule.RecordAccepted();
            requests.Add(receipt.RequestId);
            acceptances.Add(new(receipt.RequestId, schedule.LastAcceptedElapsed.Ticks));
            ValidationCheck.Require((await host.EndAsync(receipt.RequestId)).State == RequestState.Succeeded, "SoakRequestDidNotSucceed");
        }
        while (schedule.UntilEnd > TimeSpan.Zero) { await Task.Delay(schedule.UntilEnd, clock, CancellationToken.None); }
        ValidationCheck.Require(schedule.IsComplete && requests.Count >= 60, "SoakDurationOrCountNotMet");
        SoakRunEvidence evidence = new(startedUtc, clock.GetUtcNow(), schedule.Elapsed.Ticks, assemblyHash, requests, acceptances);
        await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await VerifyStoredAsync(state, host.Options, evidence);
    }

    private static async Task VerifyStoredAsync(ValidationRunState state, RuntimeOptions options, SoakRunEvidence evidence)
    {
        await using NativeLease lease = NativeLease.OpenExisting(state.Directory);
        await using ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, state.TrustScope, options, CancellationToken.None);
        await store.StartAsync(lease, CancellationToken.None);
        foreach (Guid id in evidence.RequestIds)
        {
            RequestSnapshot? request = await store.GetRequestAsync(id, CancellationToken.None);
            ValidationCheck.Require(request?.State == RequestState.Succeeded && request.Certainty == ExecutionCertainty.ConfirmedEnded, "SoakLostOriginalResult");
        }
        ValidationCheck.Require((await store.GetRuntimeStatisticsAsync(CancellationToken.None)).OccupiedReservations == 0, "SoakRetainedUnexpectedReservation");
        const string name = "soak-evidence.json";
        await lease.PrepareFileAsync(name, StateFileMode.CreateNew, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(state.Directory, name), JsonSerializer.Serialize(evidence with { ReopenedRequests = evidence.RequestIds.Count }));
    }
}

internal sealed record SoakRunEvidence(DateTimeOffset StartedUtc, DateTimeOffset EndedUtc, long ElapsedTicks, string HostAssemblySha256,
    IReadOnlyList<Guid> RequestIds, IReadOnlyList<SoakAcceptance> Acceptances)
{
    public int ReopenedRequests { get; init; }
}

internal sealed record SoakAcceptance(Guid RequestId, long ElapsedTicks);
