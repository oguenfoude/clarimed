using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace FocusMed.Data.Interceptors;

/// <summary>
/// Ensures every SQLite connection runs with WAL journal mode, a sensible busy timeout,
/// NORMAL synchronous mode, and foreign key enforcement.
/// Registered as a singleton interceptor on the DbContextOptionsBuilder.
/// </summary>
public sealed class WalModeInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragmas(connection);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ApplyPragmas(connection);
        return Task.CompletedTask;
    }

    private static void ApplyPragmas(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA busy_timeout = 5000;
            PRAGMA synchronous  = NORMAL;
            PRAGMA cache_size   = -64000;
            PRAGMA foreign_keys = ON;
            """;
        command.ExecuteNonQuery();
    }
}
