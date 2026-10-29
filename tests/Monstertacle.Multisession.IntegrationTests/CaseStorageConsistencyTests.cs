using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Opening and execution recovery evidence cannot drift from its authoritative owner and native attempt.</summary>
public sealed class CaseStorageConsistencyTests
{
    private static readonly string[] _corruptions = ["target-kind", "target-id", "SessionId", "RequestId", "DispatchId", "Revision", "Status",
        "Certainty", "BlockReason", "CreatedUtc", "Binding", "native-session", "native-head", "native-adapter", "operation", "json", "missing", "resolved", "unblocked"];

    /// <summary>Both case kinds are checked without requiring an operator read to trigger validation.</summary>
    public static IEnumerable<object[]> Corruptions()
    {
        foreach (string corruption in _corruptions)
        {
            yield return [corruption, true];
            yield return [corruption, false];
        }
    }

    /// <summary>Case corruption fails before recovery mutation while preserving the damaged file for investigation.</summary>
    [Theory]
    [MemberData(nameof(Corruptions))]
    public async Task RejectsCaseGraphDrift(string corruption, bool opening)
    {
        await using AdmissionStoreFixture fixture = await CreateCaseAsync(opening);
        CaseStoreTampering.Apply(fixture.Directory.DatabasePath, corruption);
        await RequestStorageConsistencyTests.AssertRejectedWithoutMutationAsync(fixture);
    }

    private static async Task<AdmissionStoreFixture> CreateCaseAsync(bool opening)
    {
        AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        try
        {
            SessionSnapshot session = await fixture.CreateSessionAsync();
            if (opening) { await fixture.Store.MarkSessionBlockedAsync(session.SessionId, SessionBlockReason.RecoveryRequired, CancellationToken.None); }
            else
            {
                await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "original input", TimeSpan.FromSeconds(5), null, false)), 100, CancellationToken.None);
                ExecutionReservation reservation = (await fixture.Store.TryReserveHeadAsync(4, CancellationToken.None))!;
                await fixture.Store.CommitObservationAsync(reservation.DispatchId, new(ProviderObservationKind.OperationAccepted, "operation-I"), CancellationToken.None);
                await fixture.Store.MarkUncertainAsync(reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
            }
            await fixture.Store.DisposeAsync();
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }
}
