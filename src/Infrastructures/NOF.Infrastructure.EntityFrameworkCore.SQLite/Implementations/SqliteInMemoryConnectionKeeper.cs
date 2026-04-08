using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;

namespace NOF.Infrastructure.EntityFrameworkCore.SQLite;

/// <summary>
/// Keeps named SQLite in-memory databases alive across DbContext instances.
/// </summary>
public sealed class SqliteInMemoryConnectionKeeper : IDisposable, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, SqliteConnection> _connections = new(StringComparer.Ordinal);

    public string EnsureDatabase(string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        var connectionString = CreateConnectionString(databaseName);
        _connections.GetOrAdd(connectionString, static value =>
        {
            var connection = new SqliteConnection(value);
            connection.Open();
            return connection;
        });

        return connectionString;
    }

    public void Dispose()
    {
        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }

        _connections.Clear();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private static string CreateConnectionString(string databaseName)
        => new SqliteConnectionStringBuilder
        {
            DataSource = databaseName,
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString();
}
