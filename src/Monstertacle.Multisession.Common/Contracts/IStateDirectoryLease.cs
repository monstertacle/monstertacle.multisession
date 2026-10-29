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
    /// <summary>Creates a new private regular file exclusively, or validates an existing one without changing it.</summary>
    /// <remarks>Only one relative filename is allowed. Ownership must remain held; existing files must never be replaced or repaired.</remarks>
    ValueTask PrepareFileAsync(string fileName, StateFileMode mode, CancellationToken cancellationToken);
}
