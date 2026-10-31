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
    /// <summary>The adapter durably prepared the invocation of this dispatch; it does not prove external acceptance.</summary>
    InvocationPrepared,
    /// <summary>A request-linked native session was reported; it never replaces the stable binding or authorizes resumption.</summary>
    NativeSessionObserved,
}
