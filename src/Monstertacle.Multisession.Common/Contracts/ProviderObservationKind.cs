namespace Monstertacle.Multisession.Contracts;

/// <summary>Nonterminal evidence reported by an execution adapter.</summary>
public enum ProviderObservationKind
{
    /// <summary>A stable native session identity was established.</summary>
    SessionEstablished,
    /// <summary>An external operation identifier was received.</summary>
    OperationAccepted,
    /// <summary>Content-free progress, never proof of successful completion.</summary>
    Progress,
}
