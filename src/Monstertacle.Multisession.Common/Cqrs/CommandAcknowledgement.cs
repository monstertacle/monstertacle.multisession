namespace Monstertacle.Multisession.Cqrs;

/// <summary>Confirms that the local command returned; does not confirm external termination.</summary>
public readonly record struct CommandAcknowledgement;
