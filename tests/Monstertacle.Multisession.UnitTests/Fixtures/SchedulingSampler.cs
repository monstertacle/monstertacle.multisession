using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

/// <summary>
/// Test-only diagnostic: environmental scheduling symptoms during one measured window, never runtime stages. A dedicated background
/// thread wakes on a 10 ms timeout and records how far each completed wait overslept; at a wake it queues one pool probe only when
/// none is outstanding and records the probe's queue-to-run latency. <see cref="Freeze"/> stops the thread with a bounded join,
/// discards every later write and returns one immutable snapshot. It never throws; a fault becomes part of the snapshot.
/// </summary>
/// <remarks>
/// Window: [origin, end], where end is the caller's end stamp or, when that was not observed, the freeze instant. A sample is
/// selected by its own completion stamp (wake or probe run) inside the window. A selected pool latency is the full queue-to-run
/// interval, whose queue stamp can precede the origin. Values are correlation envelopes for the window, not a cause; they need
/// not coincide with any runtime or test event. The sampler itself runs during the window: one thread every 10 ms and at most
/// one outstanding pool item. All mutable state lives in the logs and the lifecycle below, which only this type touches, and
/// only under its gate.
/// </remarks>
internal sealed class SchedulingSampler
{
    internal const int Capacity = 2048;
    private static readonly TimeSpan _period = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan _stopBound = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan _defaultLifetime = TimeSpan.FromSeconds(15);
    private readonly object _gate = new();
    private readonly TimeSpan _lifetime;
    private readonly SamplerHooks _hooks;
    private readonly WakeLog _wakes = new(Capacity);
    private readonly ProbeLog _probes = new(Capacity);
    private readonly SamplerLifecycle _lifecycle = new();
    private Thread? _thread;
    private SamplerSnapshot? _snapshot;

    // The hooks exist for private controls only and are null in every measured test. onProbeStart runs before the probe's run
    // stamp, onProbe after it and before the probe is recorded.
    internal SchedulingSampler(TimeSpan? lifetime = null, Action? onWake = null, Action? onProbe = null, Action? onProbeStart = null)
    {
        _lifetime = lifetime ?? _defaultLifetime;
        _hooks = new(onWake, onProbeStart, onProbe);
    }

    internal bool IsRunning => _thread is { IsAlive: true };

    internal int LateDiscarded { get { lock (_gate) { return _lifecycle.LateDiscarded; } } }

    internal int MaxOutstanding { get { lock (_gate) { return _probes.MaxOutstanding; } } }

    internal int Wakes { get { lock (_gate) { return _wakes.Count; } } }

    internal int Probes { get { lock (_gate) { return _probes.Count; } } }

    internal int Skipped { get { lock (_gate) { return _wakes.SkippedCount(); } } }

    internal static SchedulingSampler Started()
    {
        SchedulingSampler sampler = new();
        sampler.Start();
        return sampler;
    }

    internal void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "scheduling-sampler" };
        _thread.Start();
    }

    /// <summary>Stops sampling and returns the snapshot of [origin, end]; an end of 0 means the end stage was not observed.</summary>
    internal SamplerSnapshot Freeze(long origin, long end)
    {
        lock (_gate)
        {
            if (_snapshot is not null) { return _snapshot; }
            _lifecycle.Stopping = true;
            Monitor.PulseAll(_gate);
        }
        bool joined;
        try { joined = _thread is not { ThreadState: not System.Threading.ThreadState.Unstarted } thread || thread.Join(_stopBound); }
        catch (Exception fault)
        {
            joined = false;
            lock (_gate) { _lifecycle.Fail(fault); }
        }
        lock (_gate)
        {
            _lifecycle.Frozen = true;
            try { _snapshot = Build(origin, end == 0 ? Stopwatch.GetTimestamp() : end, end == 0, joined); }
            catch (Exception fault) { _snapshot = SamplerSnapshot.Faulted(fault.GetType().FullName ?? "unknown", joined); }
            return _snapshot;
        }
    }

    private void Run()
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            while (true)
            {
                long before = Stopwatch.GetTimestamp();
                lock (_gate)
                {
                    if (_lifecycle.Stopping || Monitor.Wait(_gate, _period) || _lifecycle.Stopping) { return; }
                }
                long woke = Stopwatch.GetTimestamp();
                if (Stopwatch.GetElapsedTime(started, woke) >= _lifetime)
                {
                    lock (_gate) { _lifecycle.Expired = true; }
                    return;
                }
                _hooks.OnWake?.Invoke();
                if (!RecordWake(woke, Stopwatch.GetElapsedTime(before, woke) - _period, out long queued)) { return; }
                if (queued != 0) { ThreadPool.UnsafeQueueUserWorkItem(static state => state.Sampler.Probe(state.Queued), (Sampler: this, Queued: queued), false); }
            }
        }
        catch (Exception fault)
        {
            lock (_gate) { _lifecycle.Fail(fault); }
        }
    }

    // False when frozen: the write is discarded and the thread ends. A non-zero queued stamp means a probe must be queued.
    private bool RecordWake(long woke, TimeSpan oversleep, out long queued)
    {
        queued = 0;
        lock (_gate)
        {
            if (_lifecycle.Frozen) { _lifecycle.LateDiscarded++; return false; }
            int index = _wakes.Add(woke, oversleep.Ticks);
            if (index < 0) { return true; }
            if (!_probes.TryBegin(out queued)) { _wakes.MarkSkipped(index); }
            return true;
        }
    }

    private void Probe(long queued)
    {
        try { _hooks.OnProbeStart?.Invoke(); }
        catch (Exception fault)
        {
            lock (_gate) { _lifecycle.Fail(fault); }
        }
        long ran = Stopwatch.GetTimestamp();
        try { _hooks.OnProbe?.Invoke(); }
        catch (Exception fault)
        {
            lock (_gate) { _lifecycle.Fail(fault); }
        }
        lock (_gate)
        {
            _probes.Complete();
            if (_lifecycle.Frozen) { _lifecycle.LateDiscarded++; return; }
            _probes.Record(queued, ran);
        }
    }

    // Called under the gate after the thread was asked to stop.
    private SamplerSnapshot Build(long origin, long end, bool endAtFreeze, bool joined)
    {
        WakeSelection wakes = _wakes.Select(origin, end);
        ProbeSelection probes = _probes.Select(origin, end);
        string status = _lifecycle.Fault is { } fault ? "fault:" + fault : _lifecycle.Expired ? "expired" : "complete";
        return new(status, endAtFreeze, joined, _wakes.Truncated || _probes.Truncated,
            wakes.Count, wakes.Count == 0 ? null : TimeSpan.FromTicks(wakes.OversleepMaxTicks).TotalMilliseconds,
            Stopwatch.GetElapsedTime(wakes.LastWake, end).TotalMilliseconds,
            probes.Count, probes.Count == 0 ? null : Stopwatch.GetElapsedTime(0, probes.LatencyMax).TotalMilliseconds,
            probes.UnresolvedSince != 0,
            probes.UnresolvedSince == 0 ? null : Stopwatch.GetElapsedTime(Math.Max(probes.UnresolvedSince, origin), end).TotalMilliseconds,
            wakes.Skipped);
    }
}

/// <summary>The private control hooks of one sampler; all null in every measured test.</summary>
internal sealed record SamplerHooks(Action? OnWake, Action? OnProbeStart, Action? OnProbe);

/// <summary>Collector state of one sampler: stop request, freeze, lifetime expiry, first fault and discarded late writes.</summary>
/// <remarks>Not synchronized itself; the owning sampler reads and writes it only under its gate.</remarks>
internal sealed class SamplerLifecycle
{
    internal bool Stopping { get; set; }

    internal bool Frozen { get; set; }

    internal bool Expired { get; set; }

    internal string? Fault { get; private set; }

    internal int LateDiscarded { get; set; }

    // Keeps the first fault only.
    internal void Fail(Exception fault) => Fault ??= fault.GetType().FullName;
}

/// <summary>Fixed-capacity log of the dedicated thread's completed waits: wake stamp, oversleep and whether no probe was queued.</summary>
/// <remarks>Not synchronized itself; the owning sampler reads and writes it only under its gate.</remarks>
internal sealed class WakeLog(int capacity)
{
    private readonly long[] _stamps = new long[capacity];
    private readonly long[] _oversleeps = new long[capacity];
    private readonly bool[] _skippedAt = new bool[capacity];

    internal int Count { get; private set; }

    internal bool Truncated { get; private set; }

    // The index of the new wake, or -1 when the log is full (then it is truncated, not grown).
    internal int Add(long stamp, long oversleepTicks)
    {
        if (Count == _stamps.Length) { Truncated = true; return -1; }
        int index = Count++;
        _stamps[index] = stamp;
        _oversleeps[index] = oversleepTicks;
        return index;
    }

    internal void MarkSkipped(int index) => _skippedAt[index] = true;

    internal int SkippedCount() => _skippedAt.Take(Count).Count(skipped => skipped);

    // Wakes are selected by their own stamp in [origin, end]; with none selected the last wake is the origin.
    internal WakeSelection Select(long origin, long end)
    {
        int count = 0;
        long oversleepMax = long.MinValue;
        long lastWake = origin;
        int skipped = 0;
        for (int index = 0; index < Count; index++)
        {
            if (_stamps[index] < origin || _stamps[index] > end) { continue; }
            count++;
            oversleepMax = Math.Max(oversleepMax, _oversleeps[index]);
            lastWake = Math.Max(lastWake, _stamps[index]);
            if (_skippedAt[index]) { skipped++; }
        }
        return new(count, oversleepMax, lastWake, skipped);
    }
}

/// <summary>The wakes selected for one window.</summary>
internal readonly record struct WakeSelection(int Count, long OversleepMaxTicks, long LastWake, int Skipped);

/// <summary>Fixed-capacity log of pool probes and the bookkeeping of the single outstanding probe.</summary>
/// <remarks>Not synchronized itself; the owning sampler reads and writes it only under its gate.</remarks>
internal sealed class ProbeLog(int capacity)
{
    private readonly long[] _queued = new long[capacity];
    private readonly long[] _ran = new long[capacity];
    private long _outstandingQueued;
    private int _outstanding;

    internal int Count { get; private set; }

    internal int MaxOutstanding { get; private set; }

    internal bool Truncated { get; private set; }

    // False while a probe is outstanding: no second probe is queued. Otherwise the new probe is outstanding from now.
    internal bool TryBegin(out long queued)
    {
        queued = 0;
        if (_outstandingQueued != 0) { return false; }
        queued = Stopwatch.GetTimestamp();
        _outstandingQueued = queued;
        MaxOutstanding = Math.Max(MaxOutstanding, ++_outstanding);
        return true;
    }

    // The probe's callback ended, whether or not it is recorded.
    internal void Complete() => _outstanding--;

    // A recorded probe is no longer outstanding; a full log is truncated, not grown.
    internal void Record(long queued, long ran)
    {
        _outstandingQueued = 0;
        if (Count == _queued.Length) { Truncated = true; return; }
        _queued[Count] = queued;
        _ran[Count++] = ran;
    }

    // Probe runs are selected by their run stamp in [origin, end]. A probe queued at or before the end and either not recorded by
    // the freeze or recorded with a run stamp after the end is unresolved for the window: a bookkeeping proxy, not proof that its
    // callback had not started.
    internal ProbeSelection Select(long origin, long end)
    {
        int count = 0;
        long latencyMax = long.MinValue;
        long unresolvedSince = 0;
        for (int index = 0; index < Count; index++)
        {
            if (_ran[index] > end && _queued[index] <= end) { unresolvedSince = _queued[index]; }
            if (_ran[index] < origin || _ran[index] > end) { continue; }
            count++;
            latencyMax = Math.Max(latencyMax, _ran[index] - _queued[index]);
        }
        if (_outstandingQueued != 0 && _outstandingQueued <= end) { unresolvedSince = _outstandingQueued; }
        return new(count, latencyMax, unresolvedSince);
    }
}

/// <summary>The probes selected for one window.</summary>
internal readonly record struct ProbeSelection(int Count, long LatencyMax, long UnresolvedSince);

/// <summary>
/// An immutable sampler result; a missing maximum means no sample was selected in the window, never zero.
/// </summary>
/// <remarks>
/// The wake age is the time from the last selected wake to the window end, or from the origin when no wake was selected; it is
/// not an oversleep and not a lower bound of one. The unresolved age is end minus the later of the probe's queue stamp and the
/// origin; it is not a dispatch latency and not a lower bound of one.
/// </remarks>
internal sealed record SamplerSnapshot(string Status, bool EndAtFreeze, bool Joined, bool Truncated,
    int DedicatedCount, double? DedicatedOversleepMaxMs, double DedicatedWakeAgeMs,
    int PoolCount, double? PoolDispatchMaxMs, bool PoolUnresolvedForWindow, double? PoolUnresolvedAgeInWindowMs, int PoolSkipped)
{
    internal static SamplerSnapshot Faulted(string fault, bool joined)
        => new("fault:" + fault, false, joined, false, 0, null, 0, 0, null, false, null, 0);

    internal string Format()
    {
        StringBuilder line = new();
        line.Append("sampler=").Append(Status)
            .Append(" window-end=").Append(EndAtFreeze ? "freeze" : "stop-failure-observed")
            .Append(" stop=").Append(Joined ? "joined" : "timeout")
            .Append(" dedicated-n=").Append(DedicatedCount.ToString(CultureInfo.InvariantCulture))
            .Append(" dedicated-oversleep-max-ms=").Append(Ms(DedicatedOversleepMaxMs))
            .Append(DedicatedCount == 0 ? " dedicated-age-since-origin-ms=" : " dedicated-last-wake-age-ms=").Append(Ms(DedicatedWakeAgeMs))
            .Append(" pool-n=").Append(PoolCount.ToString(CultureInfo.InvariantCulture))
            .Append(" pool-dispatch-max-ms=").Append(Ms(PoolDispatchMaxMs))
            .Append(" pool-unresolved-for-window=").Append(PoolUnresolvedForWindow ? "true" : "false");
        if (PoolUnresolvedForWindow) { line.Append(" pool-unresolved-age-in-window-ms=").Append(Ms(PoolUnresolvedAgeInWindowMs)); }
        line.Append(" pool-skipped=").Append(PoolSkipped.ToString(CultureInfo.InvariantCulture));
        if (Truncated) { line.Append(" truncated"); }
        return line.ToString();
    }

    private static string Ms(double? value) => value is { } ms ? ms.ToString("F1", CultureInfo.InvariantCulture) : "missing";
}
