using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Queries;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Small controlled projections only. SQLite must use native keyset queries and LIMIT.
internal static class ControlledPageProjection
{
    public static Page<T> Read<T>(IEnumerable<T> items, Func<T, CursorKey> key, PageScope scope, int pageSize, string? cursor)
    {
        PageCursor.ValidateSize(pageSize);
        CursorKey? last = PageCursor.Read(cursor, scope);
        var selected = items.Select(item => (Item: item, Key: key(item)))
            .Where(row => last is null || CursorKeyComparer.Instance.Compare(row.Key, last) > 0)
            .OrderBy(row => row.Key, CursorKeyComparer.Instance).Take(pageSize + 1).ToArray();
        string? next = selected.Length > pageSize ? PageCursor.Write(scope, selected[pageSize - 1].Key) : null;
        return new(selected.Take(pageSize).Select(row => row.Item).ToArray(), next);
    }
}
