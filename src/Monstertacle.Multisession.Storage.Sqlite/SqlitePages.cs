using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Queries;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class SqlitePages
{
    internal static Page<T> Read<T>(SqliteCommand command, int size, PageScope scope, Func<SqliteDataReader, (T Value, CursorKey Key)> decode)
    {
        using SqliteDataReader reader = command.ExecuteReader();
        List<T> items = [];
        CursorKey? last = null;
        while (reader.Read())
        {
            if (items.Count == size) { return new(items.ToArray(), PageCursor.Write(scope, last!)); }
            var (value, key) = decode(reader);
            items.Add(value);
            last = key;
        }
        return new(items.ToArray(), null);
    }
}
