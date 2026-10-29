using System.Diagnostics.Metrics;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Diagnostics;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Metrics expose finite categories and measurements without local identifiers.</summary>
public sealed class DiagnosticsMetricsTests
{
    private static readonly string[] _allowedTags = ["request.state", "execution.certainty", "error.code"];
    /// <summary>Acceptance/execution durations and result counts use only a fixed tag vocabulary.</summary>
    [Fact]
    public void ReportsDurationsAndOutcomeWithoutIdentityLabels()
    {
        List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> observed = [];
        using SessionDiagnostics diagnostics = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == SessionDiagnostics.SourceName && Equals(instrument.Meter.Scope, diagnostics.InstanceId)) { owner.EnableMeasurementEvents(instrument); }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => observed.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => observed.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();
        Guid request = Guid.NewGuid();
        Guid session = Guid.NewGuid();
        diagnostics.RecordAccepted(session, new(request, 1), TimeSpan.FromMilliseconds(25));
        diagnostics.RecordCompleted(new(Guid.NewGuid(), request, session, "SECRET-WORKSPACE", DateTimeOffset.UtcNow),
            new(RequestState.TimedOut, ExecutionCertainty.Unknown, "SECRET-OUTPUT", false, null, new(ErrorCode.TimedOut)), TimeSpan.FromSeconds(2));
        Assert.Contains(observed, item => item.Name == "multisession.admission.duration" && item.Value == 0.025);
        Assert.Contains(observed, item => item.Name == "multisession.execution.duration" && item.Value == 2);
        Assert.Contains(observed, item => item.Name == "multisession.requests.completed" && item.Value == 1);
        Assert.All(observed.SelectMany(item => item.Tags), tag => Assert.Contains(tag.Key, _allowedTags));
        Assert.DoesNotContain(observed.SelectMany(item => item.Tags), tag => tag.Value is Guid || tag.Value?.ToString()?.Contains("SECRET", StringComparison.Ordinal) == true);
    }

    /// <summary>A failing metric consumer cannot escape the confirmed acceptance boundary.</summary>
    [Fact]
    public void MetricListenerFailureIsContainedAndCounted()
    {
        using SessionDiagnostics diagnostics = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == SessionDiagnostics.SourceName && Equals(instrument.Meter.Scope, diagnostics.InstanceId) && instrument.Name == "multisession.admission.duration")
            { owner.EnableMeasurementEvents(instrument); }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => throw new InvalidOperationException("SECRET-LISTENER"));
        listener.Start();
        diagnostics.RecordAccepted(Guid.NewGuid(), new(Guid.NewGuid(), 1), TimeSpan.Zero);
        Assert.Equal(1, diagnostics.FailureCount);
    }
}
