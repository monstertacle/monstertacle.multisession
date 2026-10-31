namespace Monstertacle.Multisession.Contracts;
/// <summary>Persistent ownership of when an admitted request may begin execution.</summary>
public enum RuntimeDispatchMode
{
    /// <summary>The runtime dispatches eligible queued work automatically.</summary>
    Automatic,
    /// <summary>The host must explicitly name the eligible request to dispatch.</summary>
    HostControlled,
}
