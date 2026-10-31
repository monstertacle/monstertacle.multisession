using System.Diagnostics;
using System.Diagnostics.Metrics;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Diagnostics;

// Meter scope distinguishes host instances without adding identity labels to measurements.
internal sealed class SessionMeasurements : IDisposable
{
    private readonly Meter _meter;
    private readonly Histogram<double> _admission;
    private readonly Histogram<double> _execution;
    private readonly Counter<long> _completed;
    private readonly Action _failed;
    private RuntimeStatistics? _statistics;

    internal SessionMeasurements(Guid scope, Action failed)
    {
        _failed = failed;
        _meter = new(new MeterOptions(SessionDiagnostics.SourceName) { Scope = scope });
        _admission = _meter.CreateHistogram<double>("multisession.admission.duration", "s", "Duration of confirmed durable admission.");
        _execution = _meter.CreateHistogram<double>("multisession.execution.duration", "s", "Duration from reservation to confirmed local completion.");
        _completed = _meter.CreateCounter<long>("multisession.requests.completed", "{request}", "Acknowledged local terminal results.");
        _meter.CreateObservableGauge("multisession.requests.queued", () => Observe(item => item.QueuedRequests), "{request}");
        _meter.CreateObservableGauge("multisession.reservations.occupied", () => Observe(item => item.OccupiedReservations), "{reservation}");
        _meter.CreateObservableGauge("multisession.reservations.uncertain", () => Observe(item => item.UncertainReservations), "{reservation}");
    }

    internal void Acceptance(TimeSpan elapsed) => Safely(() => _admission.Record(Math.Max(0, elapsed.TotalSeconds)));
    internal void Update(RuntimeStatistics? statistics) => Volatile.Write(ref _statistics, statistics);
    private Measurement<long>[] Observe(Func<RuntimeStatistics, long> project)
        => Volatile.Read(ref _statistics) is { } snapshot ? [new(project(snapshot))] : [];
    internal void Completion(ProviderReply reply, TimeSpan elapsed)
    {
        TagList tags = new()
        {
            { "request.state", SessionDiagnostics.Name(reply.Outcome) },
            { "execution.certainty", SessionDiagnostics.Name(reply.Certainty) },
            { "error.code", reply.Error is null ? "None" : SessionDiagnostics.Name(reply.Error.Code) }
        };
        Safely(() => _execution.Record(Math.Max(0, elapsed.TotalSeconds), tags));
        Safely(() => _completed.Add(1, tags));
    }

    private void Safely(Action write)
    {
        try { write(); }
        catch (Exception) { _failed(); }
    }
    public void Dispose() => _meter.Dispose();
}
