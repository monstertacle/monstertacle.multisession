namespace Monstertacle.Multisession.Contracts;

/// <summary>Resolution status independent from request outcome.</summary>
public enum RecoveryCaseStatus
{
    /// <summary>Investigation or an explicit decision remains necessary.</summary>
    Open,
    /// <summary>Explicit evidence and a revision-bound decision were committed.</summary>
    Resolved,
}
