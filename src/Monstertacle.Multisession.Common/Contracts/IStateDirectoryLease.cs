namespace Monstertacle.Multisession.Contracts;

/// <summary>Validated native ownership held for the full lifetime of store usage.</summary>
public interface IStateDirectoryLease : IAsyncDisposable
{
    /// <summary>Gets the validated absolute private state directory.</summary>
    string StateDirectory { get; }
}
