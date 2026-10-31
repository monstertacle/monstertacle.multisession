using System.Diagnostics;
using System.Text.Json;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>The serial SQLite executor must remain schedulable while unrelated managed workers are blocked.</summary>
public sealed class StoreSchedulerTests
{
    /// <summary>Racing producers and repeated disposers terminate without collection-lifetime exceptions.</summary>
    [Fact]
    public async Task CompletesRacingReadsAndRepeatedDisposal()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task[] readers = Enumerable.Range(0, 32).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            try { await fixture.Store.GetSessionAsync(Guid.NewGuid(), CancellationToken.None); }
            catch (Monstertacle.Multisession.Contracts.SessionOperationException failure)
            { Assert.Equal(Monstertacle.Multisession.Contracts.ErrorCode.StorageUnavailable, failure.Failure.Code); }
        })).ToArray();
        Task[] disposers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            await fixture.Store.DisposeAsync();
        })).ToArray();
        start.SetResult();
        await Task.WhenAll(readers.Concat(disposers)).WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Store.DisposeAsync();
    }

    /// <summary>The single blocked worker is confined to a child, preserving the test runner's scheduler.</summary>
    [Fact]
    public async Task IdleStoreReadDoesNotDependOnAvailableManagedWorkers()
    {
        await using PrivateStoreDirectory parent = new();
        ProcessStartInfo start = new("dotnet");
        foreach (string argument in new[] { ValidationHostBuild.WorkerPath(), "--store-scheduler-probe", Path.Combine(parent.StateDirectory, "scheduler") })
        { start.ArgumentList.Add(argument); }
        ProbeProcessResult result = await ProbeProcess.RunAsync(start);
        Assert.True(result.ExitCode == 0, result.Output);
        using JsonDocument report = JsonDocument.Parse(result.Output);
        Assert.True(report.RootElement.GetProperty("IndependentStoreRead").GetBoolean());
    }
}
