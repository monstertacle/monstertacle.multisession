using System.Diagnostics.Metrics;
using Monstertacle.Multisession.Diagnostics;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

internal sealed class MetricGaugeCapture : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly Dictionary<string, long> _values = [];
    internal MetricGaugeCapture(Guid scope)
    {
        _listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.IsObservable && instrument.Meter.Name == SessionDiagnostics.SourceName && Equals(instrument.Meter.Scope, scope))
            { owner.EnableMeasurementEvents(instrument); }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => _values[instrument.Name] = value);
        _listener.Start();
    }
    internal IReadOnlyDictionary<string, long> Read()
    {
        _values.Clear();
        _listener.RecordObservableInstruments();
        return new Dictionary<string, long>(_values);
    }
    public void Dispose() => _listener.Dispose();
}
