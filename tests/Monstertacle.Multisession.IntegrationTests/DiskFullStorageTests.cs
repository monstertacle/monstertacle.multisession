using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.ValidationHost;
using Xunit;
using Xunit.Abstractions;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>A real small full filesystem cannot confirm admission, erase a prior result or send new work.</summary>
[Collection("Exclusive storage volume")]
public sealed class DiskFullStorageTests(ITestOutputHelper output)
{
    /// <summary>The child proves actual OS no-space pressure, then checks the public runtime and restored durable contents.</summary>
    [Fact]
    public async Task FullPrivateVolumePreservesPriorResultsAndRejectsNewAdmission()
    {
        await using PersistedDispatchFixture seed = await PersistedDispatchFixture.CreateAsync();
        await seed.Host.Store.CommitCompletionAsync(seed.Reservation.DispatchId, seed.Reply(), CancellationToken.None);
        await seed.Host.Store.DisposeAsync();
        ProbeProcessResult result = await DiskFullHarness.RunAsync(seed);
        output.WriteLine(result.Output);
        Assert.True(result.ExitCode == 0, "Disk-full worker failed: " + result.Output + result.Error);
        DiskFullReport report = JsonSerializer.Deserialize<DiskFullReport>(result.Output.Trim())!;
        Assert.True(report.Verified);
        Assert.Equal("verify", report.Stage);
        Assert.Equal(ErrorCode.StorageUnavailable, report.Failure?.Code);
        Assert.NotEqual(AdmissionStatus.Committed, report.Failure?.AdmissionStatus);
        Assert.True(report.FilledBytes > 0 && report.FilledBytes <= DiskFullProtocol.MaximumVolumeBytes);
        Assert.True(DiskFullProtocol.IsFullCode(report.FullCode));
    }
}
