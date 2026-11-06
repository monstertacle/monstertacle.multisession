using System.Globalization;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

/// <summary>
/// The last <c>capacity</c> characters of a child's standard error (T023, SDK-SQ-001), fed in bounded chunks by the reader. Storage
/// is one fixed ring of <c>capacity</c> characters: a chunk never grows it, and an older prefix is overwritten. Every decoded
/// character is counted, line terminators included as decoded; nothing older than the tail is kept.
/// </summary>
internal sealed class BoundedTextTail(int capacity)
{
    private readonly Lock _gate = new();
    private readonly char[] _ring = new char[capacity];
    private int _oldest;
    private int _stored;
    private int _peak;
    private long _seen;

    // The most characters the tail ever stored (SDK-SQ-001 control); never above the capacity.
    internal int PeakStored { get { lock (_gate) { return _peak; } } }

    internal void Append(ReadOnlySpan<char> chunk)
    {
        lock (_gate)
        {
            _seen += chunk.Length;
            if (chunk.Length >= capacity)
            {
                chunk[^capacity..].CopyTo(_ring);
                _oldest = 0;
                _stored = capacity;
            }
            else
            {
                foreach (char character in chunk)
                {
                    _ring[(_oldest + _stored) % capacity] = character;
                    if (_stored < capacity) { _stored++; }
                    else { _oldest = (_oldest + 1) % capacity; }
                }
            }
            _peak = Math.Max(_peak, _stored);
        }
    }

    internal string Describe()
    {
        lock (_gate)
        {
            if (_seen == 0) { return "standard error: none"; }
            char[] tail = new char[_stored];
            for (int index = 0; index < _stored; index++) { tail[index] = _ring[(_oldest + index) % capacity]; }
            return "standard error: " + _seen.ToString(CultureInfo.InvariantCulture) + " chars, last " + _stored.ToString(CultureInfo.InvariantCulture) + ": " + new string(tail);
        }
    }
}
