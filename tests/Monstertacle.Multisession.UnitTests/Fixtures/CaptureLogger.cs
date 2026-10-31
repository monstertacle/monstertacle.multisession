using Microsoft.Extensions.Logging;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

internal sealed class CaptureLogger : ILogger
{
    internal List<Dictionary<string, object?>> Events { get; } = [];
    internal List<Exception?> Exceptions { get; } = [];
    internal bool Throw { get; init; }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (Throw) { throw new InvalidOperationException("SECRET-LOGGER-FAILURE"); }
        IEnumerable<KeyValuePair<string, object?>> values = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(state);
        lock (Events)
        {
            Events.Add(values.ToDictionary());
            Exceptions.Add(exception);
        }
        Assert.DoesNotContain("SECRET", formatter(state, exception));
    }
}
