using System.Diagnostics.Metrics;
using Monstertacle.Multisession.Diagnostics;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Observable gauges read only cached authoritative snapshots.</summary>
public sealed class DiagnosticsGaugeTests
{
    /// <summary>Independent runtime sources never mix cached occupancy despite sharing the standard meter name.</summary>
    [Fact]
    public void DiagnosticScopesKeepTheirStatisticsSeparate()
    {
        using SessionDiagnostics first = new();
        using SessionDiagnostics second = new();
        using MetricGaugeCapture firstMetrics = new(first.InstanceId);
        using MetricGaugeCapture secondMetrics = new(second.InstanceId);
        first.UpdateRuntimeStatistics(new(3, 2, 1));
        second.UpdateRuntimeStatistics(new(9, 8, 7));
        Assert.Equal(3, firstMetrics.Read()["multisession.requests.queued"]);
        Assert.Equal(9, secondMetrics.Read()["multisession.requests.queued"]);
        first.UpdateRuntimeStatistics(null);
        Assert.Empty(firstMetrics.Read());
        Assert.Equal(7, secondMetrics.Read()["multisession.reservations.uncertain"]);
    }

    /// <summary>Unknown availability is not zero; unknown reservations remain part of occupied slots.</summary>
    [Fact]
    public void GaugesPublishCachedSnapshotAndWithdrawUnavailableData()
    {
        using SessionDiagnostics diagnostics = new();
        Dictionary<string, long> observed = [];
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == SessionDiagnostics.SourceName && Equals(instrument.Meter.Scope, diagnostics.InstanceId))
            { owner.EnableMeasurementEvents(instrument); }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            Assert.True(tags.IsEmpty);
            observed[instrument.Name] = value;
        });
        listener.Start();
        listener.RecordObservableInstruments();
        Assert.Empty(observed);
        diagnostics.UpdateRuntimeStatistics(new(3, 2, 1));
        listener.RecordObservableInstruments();
        Assert.Equal(3, observed["multisession.requests.queued"]);
        Assert.Equal(2, observed["multisession.reservations.occupied"]);
        Assert.Equal(1, observed["multisession.reservations.uncertain"]);
        observed.Clear();
        diagnostics.UpdateRuntimeStatistics(null);
        listener.RecordObservableInstruments();
        Assert.Empty(observed);
        diagnostics.Dispose();
        listener.RecordObservableInstruments();
        Assert.Empty(observed);
    }
}
