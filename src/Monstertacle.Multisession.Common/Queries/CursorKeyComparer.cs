namespace Monstertacle.Multisession.Queries;

/// <summary>Compares complete keys using UTC/ordinal values and canonical ordinal UUID strings.</summary>
public sealed class CursorKeyComparer : IComparer<CursorKey>
{
    /// <summary>Gets the stateless key comparer.</summary>
    public static CursorKeyComparer Instance { get; } = new();
    /// <inheritdoc />
    public int Compare(CursorKey? left, CursorKey? right)
    {
        if (ReferenceEquals(left, right)) { return 0; }
        if (left is null) { return -1; }
        if (right is null) { return 1; }
        int result = left.Utc is not null || right.Utc is not null ? Nullable.Compare(left.Utc, right.Utc) : Nullable.Compare(left.Ordinal, right.Ordinal);
        if (result == 0) { result = Nullable.Compare(left.TargetKind, right.TargetKind); }
        return result == 0 ? string.CompareOrdinal(left.Id.ToString("D"), right.Id.ToString("D")) : result;
    }
}
