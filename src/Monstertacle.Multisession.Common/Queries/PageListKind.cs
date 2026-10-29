namespace Monstertacle.Multisession.Queries;

/// <summary>The explicitly bound list type of a cursor.</summary>
public enum PageListKind
{
    /// <summary>Sessions ordered by UTC creation time and local identity.</summary>
    Sessions,
    /// <summary>Session requests ordered by accepted sequence and identity.</summary>
    Requests,
    /// <summary>Session history ordered by history ordinal and identity.</summary>
    History,
    /// <summary>Open recovery cases ordered by UTC creation, target kind and identity.</summary>
    Recovery,
}
