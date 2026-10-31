namespace Monstertacle.Multisession.Contracts;

/// <summary>The explicit intent for a file within an owned private state directory.</summary>
public enum StateFileMode
{
    /// <summary>Create a private regular file exclusively, failing if anything already exists.</summary>
    CreateNew,
    /// <summary>Require an existing private regular file, without changing contents or permissions.</summary>
    OpenExisting,
}
