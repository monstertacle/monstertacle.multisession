using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Tests the actual scenario and wave owners, not a parallel generic resource runner.</summary>
public sealed class ScenarioOwnershipTests
{
    /// <summary>A failed real wave plus failed runtime cleanup and failed state cleanup retains every visible failure.</summary>
    [Fact]
    public async Task PreservesWaveFailureAcrossBothCleanupBoundaries()
    {
        RuntimeOptions options = RuntimeOptionsTests.ValidOptions();
        int storeDisposals = 0;
        InMemorySessionStore store = new()
        {
            Disposing = () =>
            {
                storeDisposals++;
                return ValueTask.FromException(new SessionOperationException(new(ErrorCode.StorageUnavailable)));
            },
        };
        SessionRuntime runtime = new(store, new TestStateDirectoryLease(options.StateDirectory), [], []);
        await runtime.StartAsync(options, CancellationToken.None);
        ProbeHost host = new(store, runtime, options);
        FailingProbeLease lease = new(options.StateDirectory, new SessionOperationException(new(ErrorCode.InvalidConfiguration)));
        Guid runId = Guid.NewGuid();
        ValidationReport report = await ScenarioRunner.ExecuteOwnedAsync("deterministic", new(runId, lease),
            _ => WaveProbeFlow.ExecuteOwnedAsync(host, new(), new(2), "deterministic"));
        Assert.Equal("FAIL", report.Status);
        Assert.Equal(ErrorCode.NotFound, report.Failure?.Error.Code);
        Assert.Equal(new[] { ErrorCode.StorageUnavailable, ErrorCode.InvalidConfiguration },
            report.CleanupFailures.Select(item => item.Error.Code));
        Assert.Equal(runId, report.RunId);
        Assert.Equal(1, storeDisposals);
        Assert.Equal(1, lease.Disposals);
    }

    /// <summary>Cleanup-only is a failure with no invented primary execution fault.</summary>
    [Fact]
    public async Task SeparatesCleanupOnlyFailure()
    {
        FailingProbeLease lease = new(Path.GetTempPath(), new IOException("private-cleanup-marker"));
        ValidationReport report = await ScenarioRunner.ExecuteOwnedAsync("fixture", new(Guid.NewGuid(), lease), _ => Task.CompletedTask);
        Assert.Equal("FAIL", report.Status);
        Assert.Equal("ScenarioCleanupFailed", report.Reason);
        Assert.Null(report.Failure);
        Assert.Equal(ErrorCode.ExecutionFailed, Assert.Single(report.CleanupFailures).Error.Code);
        Assert.DoesNotContain("private-cleanup-marker", JsonSerializer.Serialize(report), StringComparison.Ordinal);
        Assert.Equal(1, lease.Disposals);
    }

    /// <summary>Known primary diagnostics survive successful cleanup, with their original certainty.</summary>
    [Fact]
    public async Task PreservesPrimaryDetailsAndRunIdentity()
    {
        FailureDetails details = new(FailureStage.StoreCommit, 19, 787);
        PublicError error = new(ErrorCode.StorageUnavailable, AdmissionStatus: AdmissionStatus.Unknown);
        FailingProbeLease lease = new(Path.GetTempPath());
        Guid runId = Guid.NewGuid();
        ValidationReport report = await ScenarioRunner.ExecuteOwnedAsync("fixture", new(runId, lease),
            _ => throw new SessionOperationException(error) { Details = details });
        Assert.Equal(error, report.Failure?.Error);
        Assert.Equal(details, report.Failure?.Details);
        Assert.Equal(runId, report.RunId);
        Assert.Empty(report.CleanupFailures);
        Assert.Equal(1, lease.Disposals);
    }

    /// <summary>Failed startup does not lose its safe cause when the host-owned store also fails cleanup.</summary>
    [Fact]
    public async Task PreservesStartupFailureWhileAttemptingStoreCleanup()
    {
        RuntimeOptions options = RuntimeOptionsTests.ValidOptions();
        FailureDetails details = new(FailureStage.StoreOpen, 14, 14);
        int disposals = 0;
        InMemorySessionStore store = new()
        {
            ValidatingOptions = (_, _) => throw new SessionOperationException(new(ErrorCode.InvalidConfiguration)) { Details = details },
            Disposing = () => { disposals++; return ValueTask.FromException(new IOException("private-cleanup-marker")); },
        };
        SessionRuntime runtime = new(store, new TestStateDirectoryLease(options.StateDirectory), [], []);
        ValidationProbeException failure = await Assert.ThrowsAsync<ValidationProbeException>(() =>
            ProbeHost.StartPreparedAsync(store, runtime, options));
        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure?.Error.Code);
        Assert.Equal(details, failure.Failure?.Details);
        Assert.Equal(ErrorCode.ExecutionFailed, Assert.Single(failure.CleanupFailures).Error.Code);
        Assert.Equal(1, disposals);
        Assert.DoesNotContain("private-", JsonSerializer.Serialize(failure.Failure), StringComparison.Ordinal);
    }

    /// <summary>Nested cleanup-only payloads contribute their actual failures without an invented wrapper failure.</summary>
    [Fact]
    public async Task FlattensCleanupOnlyEvidenceExactlyOnce()
    {
        ValidationFailure cleanup = new(new(ErrorCode.StorageUnavailable), new(FailureStage.StoreOpen, 14, 14));
        ValidationProbeException nested = new("ScenarioCleanupFailed", cleanupFailures: [cleanup]);
        FailingProbeLease lease = new(Path.GetTempPath(), nested);
        ValidationReport report = await ScenarioRunner.ExecuteOwnedAsync("fixture", new(Guid.NewGuid(), lease), _ => Task.CompletedTask);
        Assert.Null(report.Failure);
        Assert.Equal(cleanup, Assert.Single(report.CleanupFailures));
        ValidationProbeException primary = ValidationProbeException.FromException(new SessionOperationException(new(ErrorCode.InvalidConfiguration)));
        ValidationProbeException combined = primary.WithCleanup(nested);
        Assert.Equal(primary.Failure, combined.Failure);
        Assert.Equal(cleanup, Assert.Single(combined.CleanupFailures));
    }
}
