namespace Monstertacle.Multisession.Contracts;

/// <summary>Common bounds for every public list and continuation cursor.</summary>
public static class PageLimits
{
    /// <summary>The inclusive minimum page size.</summary>
    public const int MinimumSize = 1;
    /// <summary>The inclusive maximum page size.</summary>
    public const int MaximumSize = 100;
    /// <summary>The recommended host-selected size, not an implicit API default.</summary>
    public const int RecommendedSize = 50;
    /// <summary>The maximum UTF-8 cursor length in bytes.</summary>
    public const int MaximumCursorBytes = 2048;
    /// <summary>The only currently supported cursor format.</summary>
    public const int CursorVersion = 1;
}
