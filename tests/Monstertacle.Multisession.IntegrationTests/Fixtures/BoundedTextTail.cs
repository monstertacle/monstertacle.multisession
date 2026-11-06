using System.Globalization;
using System.Text;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

/// <summary>
/// The last <c>capacity</c> characters of a child's standard error (T023), appended from the process's asynchronous reader, so a
/// full pipe never blocks the child. It counts every character seen; nothing older than the tail is kept.
/// </summary>
internal sealed class BoundedTextTail(int capacity)
{
    private readonly Lock _gate = new();
    private readonly StringBuilder _tail = new();
    private long _seen;
    private int _peak;

    // The most characters the tail ever stored (SDK-SQ-001 control).
    internal int PeakStored { get { lock (_gate) { return _peak; } } }

    internal void Append(string? line)
    {
        if (line is null) { return; }
        lock (_gate)
        {
            _seen += line.Length + 1;
            _tail.Append(line).Append('\n');
            _peak = Math.Max(_peak, _tail.Length);
            if (_tail.Length > capacity) { _tail.Remove(0, _tail.Length - capacity); }
        }
    }

    internal string Describe()
    {
        lock (_gate)
        {
            return _seen == 0
                ? "standard error: none"
                : "standard error: " + _seen.ToString(CultureInfo.InvariantCulture) + " chars, last " + _tail.Length.ToString(CultureInfo.InvariantCulture) + ": " + _tail;
        }
    }
}
