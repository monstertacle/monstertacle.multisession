namespace Monstertacle.Multisession.Contracts;

/// <summary>Validated native ownership held for the full lifetime of store usage.</summary>
public interface IStateDirectoryLease : IAsyncDisposable
{
    /// <summary>Gets whether native ownership is still held.</summary>
    bool IsHeld { get; }
    /// <summary>Checks a directory against this lease using native canonical path rules.</summary>
    bool Matches(string stateDirectory);
    /// <summary>Gets the validated absolute private state directory.</summary>
    string StateDirectory { get; }
}
