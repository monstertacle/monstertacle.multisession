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
/// one outstanding pool item.
/// </remarks>
internal sealed class SchedulingSampler
{
    internal const int Capacity = 2048;
    private static readonly TimeSpan _period = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan _stopBound = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan _defaultLifetime = TimeSpan.FromSeconds(15);
    private readonly object _gate = new();
    private readonly TimeSpan _lifetime;
    private readonly Action? _onWake;
    private readonly Action? _onProbeStart;
    private readonly Action? _onProbe;
    private readonly long[] _wakeStamps = new long[Capacity];
    private readonly long[] _oversleeps = new long[Capacity];
    private readonly bool[] _skippedAt = new bool[Capacity];
    private readonly long[] _probeQueued = new long[Capacity];
    private readonly long[] _probeRan = new long[Capacity];
    private int _wakes;
    private int _probes;
    private bool _truncated;
    private bool _stopping;
    private bool _frozen;
    private bool _expired;
    private string? _fault;
    private long _outstandingQueued;
    private int _outstanding;
    private int _maxOutstanding;
    private int _lateDiscarded;
    private Thread? _thread;
    private SamplerSnapshot? _snapshot;

    // The hooks exist for private controls only and are null in every measured test. onProbeStart runs before the probe's run
    // stamp, onProbe after it and before the probe is recorded.
    internal SchedulingSampler(TimeSpan? lifetime = null, Action? onWake = null, Action? onProbe = null, Action? onProbeStart = null)
    {
        _lifetime = lifetime ?? _defaultLifetime;
        _onWake = onWake;
        _onProbe = onProbe;
        _onProbeStart = onProbeStart;
    }

    internal bool IsRunning => _thread is { IsAlive: true };

    internal int LateDiscarded { get { lock (_gate) { return _lateDiscarded; } } }

    internal int MaxOutstanding { get { lock (_gate) { return _maxOutstanding; } } }

    internal int Wakes { get { lock (_gate) { return _wakes; } } }

    internal int Probes { get { lock (_gate) { return _probes; } } }

    internal int Skipped { get { lock (_gate) { return _skippedAt.Take(_wakes).Count(skipped => skipped); } } }

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
            _stopping = true;
            Monitor.PulseAll(_gate);
        }
        bool joined;
        try { joined = _thread is not { ThreadState: not System.Threading.ThreadState.Unstarted } thread || thread.Join(_stopBound); }
        catch (Exception fault)
        {
            joined = false;
            lock (_gate) { _fault ??= fault.GetType().FullName; }
        }
        lock (_gate)
        {
            _frozen = true;
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
                    if (_stopping || Monitor.Wait(_gate, _period) || _stopping) { return; }
                }
                long woke = Stopwatch.GetTimestamp();
                if (Stopwatch.GetElapsedTime(started, woke) >= _lifetime)
                {
                    lock (_gate) { _expired = true; }
                    return;
                }
                _onWake?.Invoke();
                if (!RecordWake(woke, Stopwatch.GetElapsedTime(before, woke) - _period, out long queued)) { return; }
                if (queued != 0) { ThreadPool.UnsafeQueueUserWorkItem(static state => state.Sampler.Probe(state.Queued), (Sampler: this, Queued: queued), false); }
            }
        }
        catch (Exception fault)
        {
            lock (_gate) { _fault ??= fault.GetType().FullName; }
        }
    }

    // False when frozen: the write is discarded and the thread ends. A non-zero queued stamp means a probe must be queued.
    private bool RecordWake(long woke, TimeSpan oversleep, out long queued)
    {
        queued = 0;
        lock (_gate)
        {
            if (_frozen) { _lateDiscarded++; return false; }
            if (_wakes == Capacity) { _truncated = true; return true; }
            int index = _wakes++;
            _wakeStamps[index] = woke;
            _oversleeps[index] = oversleep.Ticks;
            if (_outstandingQueued != 0) { _skippedAt[index] = true; return true; }
            queued = Stopwatch.GetTimestamp();
            _outstandingQueued = queued;
            _maxOutstanding = Math.Max(_maxOutstanding, ++_outstanding);
            return true;
        }
    }

    private void Probe(long queued)
    {
        try { _onProbeStart?.Invoke(); }
        catch (Exception fault)
        {
            lock (_gate) { _fault ??= fault.GetType().FullName; }
        }
        long ran = Stopwatch.GetTimestamp();
        try { _onProbe?.Invoke(); }
        catch (Exception fault)
        {
            lock (_gate) { _fault ??= fault.GetType().FullName; }
        }
        lock (_gate)
        {
            _outstanding--;
            if (_frozen) { _lateDiscarded++; return; }
            _outstandingQueued = 0;
            if (_probes == Capacity) { _truncated = true; return; }
            _probeQueued[_probes] = queued;
            _probeRan[_probes++] = ran;
        }
    }

    // Called under the gate after the thread was asked to stop. Wakes and probe runs are selected by their own stamp in
    // [origin, end]. A probe queued at or before the end and either not recorded by the freeze or recorded with a run stamp after
    // the end is unresolved for the window: a bookkeeping proxy, not proof that its callback had not started.
    private SamplerSnapshot Build(long origin, long end, bool endAtFreeze, bool joined)
    {
        int dedicated = 0;
        long oversleepMax = long.MinValue;
        long lastWake = origin;
        int skipped = 0;
        for (int index = 0; index < _wakes; index++)
        {
            if (_wakeStamps[index] < origin || _wakeStamps[index] > end) { continue; }
            dedicated++;
            oversleepMax = Math.Max(oversleepMax, _oversleeps[index]);
            lastWake = Math.Max(lastWake, _wakeStamps[index]);
            if (_skippedAt[index]) { skipped++; }
        }
        int pool = 0;
        long latencyMax = long.MinValue;
        long unresolvedSince = 0;
        for (int index = 0; index < _probes; index++)
        {
            if (_probeRan[index] > end && _probeQueued[index] <= end) { unresolvedSince = _probeQueued[index]; }
            if (_probeRan[index] < origin || _probeRan[index] > end) { continue; }
            pool++;
            latencyMax = Math.Max(latencyMax, _probeRan[index] - _probeQueued[index]);
        }
        if (_outstandingQueued != 0 && _outstandingQueued <= end) { unresolvedSince = _outstandingQueued; }
        string status = _fault is not null ? "fault:" + _fault : _expired ? "expired" : "complete";
        return new(status, endAtFreeze, joined, _truncated,
            dedicated, dedicated == 0 ? null : TimeSpan.FromTicks(oversleepMax).TotalMilliseconds,
            Stopwatch.GetElapsedTime(lastWake, end).TotalMilliseconds,
            pool, pool == 0 ? null : Stopwatch.GetElapsedTime(0, latencyMax).TotalMilliseconds,
            unresolvedSince != 0, unresolvedSince == 0 ? null : Stopwatch.GetElapsedTime(Math.Max(unresolvedSince, origin), end).TotalMilliseconds,
            skipped);
    }
}

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
