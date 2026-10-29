using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
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
}
