using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Finite worker categories explain failures without exposing exception payloads.</summary>
public sealed class CrashWorkerDiagnosticTests
{
    /// <summary>An invalid store is reported as storage failure rather than an unexplained absent barrier.</summary>
    [Fact]
    public async Task ReportsStorageCategoryForInvalidWorkerStore()
    {
        await using PrivateStoreDirectory directory = new();
        await directory.PrepareFileAsync("multisession.db", StateFileMode.CreateNew, CancellationToken.None);
        await File.WriteAllTextAsync(directory.DatabasePath, "invalid database secret sentinel");
        await File.WriteAllTextAsync(Path.Combine(directory.StateDirectory, CrashProtocol.ScopeFileName), directory.TrustScopeId.ToString("D"));
        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using CrashHarness worker = await CrashHarness.StartAsync(directory, CrashProtocol.RecoveryPoint);
        });
        Assert.Contains("UnsupportedStoreVersion", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret sentinel", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.StateDirectory, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>After the worker is killed, a reported candidate is looked up read-only: a stored request shows its state, an unknown one is absent.</summary>
    [Fact]
    public async Task LooksUpAReportedCandidateWithoutResubmitting()
    {
        await using PrivateStoreDirectory directory = new();
        await (await SqliteStoreFactory.CreateNewAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None)).DisposeAsync();
        await directory.PrepareFileAsync(CrashProtocol.ScopeFileName, StateFileMode.CreateNew, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(directory.StateDirectory, CrashProtocol.ScopeFileName), directory.TrustScopeId.ToString("D"));
        await using CrashHarness child = await CrashHarness.StartAsync(directory, CrashProtocol.RecoveryPoint);
        CrashStateManifest manifest = child.Signal!.State!;
        await child.KillAsync();

        Assert.Equal(" candidate " + manifest.CompletedA.ToString("D") + ": state=Succeeded certainty=ConfirmedEnded", CrashHarness.DescribeCandidate(directory.DatabasePath, manifest.CompletedA));
        Guid unknown = Guid.NewGuid();
        Assert.Equal(" candidate " + unknown.ToString("D") + ": absent", CrashHarness.DescribeCandidate(directory.DatabasePath, unknown));
        Assert.Equal(string.Empty, CrashHarness.DescribeCandidate(directory.DatabasePath, null));
    }
}
