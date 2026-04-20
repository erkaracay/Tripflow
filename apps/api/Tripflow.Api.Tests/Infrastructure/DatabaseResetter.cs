using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tripflow.Api.Data;

namespace Tripflow.Api.Tests.Infrastructure;

/// <summary>
/// Truncates all tables between tests so each case starts on a clean slate.
/// Keeps the EF migration history table and the data-protection key ring
/// because those are applied once per container and are expensive to rebuild.
/// </summary>
public static class DatabaseResetter
{
    private static readonly HashSet<string> SkipTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "__EFMigrationsHistory",
        "DataProtectionKeys",
    };

    public static async Task ResetAsync(TripflowDbContext db, CancellationToken ct = default)
    {
        var tableNames = await LoadTableNamesAsync(db, ct);
        var truncatable = tableNames
            .Where(t => !SkipTables.Contains(t))
            .Select(t => $"\"{t}\"")
            .ToList();

        if (truncatable.Count == 0)
        {
            return;
        }

        var sql = $"TRUNCATE TABLE {string.Join(", ", truncatable)} RESTART IDENTITY CASCADE;";
        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private static async Task<List<string>> LoadTableNamesAsync(TripflowDbContext db, CancellationToken ct)
    {
        var result = new List<string>();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT tablename
            FROM pg_tables
            WHERE schemaname = 'public';
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
}
