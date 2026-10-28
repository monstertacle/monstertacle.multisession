using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession;

/// <summary>Public runtime signatures; lifecycle behavior is implemented only after its Red tests.</summary>
public sealed class SessionRuntime : IAsyncDisposable
{
    /// <summary>Gets session operations once a runtime has been composed.</summary>
    public ISessions Sessions => throw new NotImplementedException();
    /// <summary>Gets request operations once a runtime has been composed.</summary>
    public IRequests Requests => throw new NotImplementedException();
    /// <summary>Gets explicit recovery operations once a runtime has been composed.</summary>
    public IRecovery Recovery => throw new NotImplementedException();
    /// <summary>Starts validated state ownership; not implemented by this signature scaffold.</summary>
    public ValueTask StartAsync(RuntimeOptions options, CancellationToken cancellationToken) => throw new NotImplementedException();
    /// <summary>Stops acceptance and preserves visible state; not implemented by this scaffold.</summary>
    public ValueTask StopAsync(TimeSpan deadline, CancellationToken cancellationToken) => throw new NotImplementedException();
    /// <summary>Releases resources after controlled stop; not implemented by this scaffold.</summary>
    public ValueTask DisposeAsync() => throw new NotImplementedException();
}
