using DrugCompare.Infrastructure.SQLite;
using DrugCompare.Application.Models;
using DrugCompare.Application.Repositories.Contracts;
using Microsoft.Data.Sqlite;

namespace DrugCompare.Infrastructure.SQLite;

public sealed class SqliteInteractionHistoryRepository : IInteractionHistoryRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteInteractionHistoryRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SaveInteractionCheckAsync(
        IReadOnlyCollection<ActiveSubstanceItem> substances,
        IReadOnlyCollection<InteractionResult> results)
    {
        const string sql = """
            INSERT INTO interaction_history (
                accepted_substances_text,
                results_text,
                highest_severity,
                created_at
            )
            VALUES (
                @accepted_substances_text,
                @results_text,
                @highest_severity,
                datetime('now')
            );
            """;

        var acceptedSubstancesText = string.Join(
            ", ",
            substances.Select(x =>
                string.IsNullOrWhiteSpace(x.DDInterId)
                    ? x.Name
                    : $"{x.Name} ({x.DDInterId})"));

        var resultsText = results.Count == 0
            ? "No known interaction was found in the local database. Missing interaction data does not mean that the combination is safe."
            : string.Join(
                " | ",
                results.Select(x =>
                    $"{x.SubstanceA} + {x.SubstanceB}: {x.Severity}"));

        var highestSeverity = results.Count == 0
            ? "None"
            : results
                .OrderByDescending(x => GetSeverityScore(x.Severity))
                .First()
                .Severity;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await EnsureTableExistsAsync(connection);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@accepted_substances_text", acceptedSubstancesText);
        command.Parameters.AddWithValue("@results_text", resultsText);
        command.Parameters.AddWithValue("@highest_severity", highestSeverity);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<InteractionHistoryItem>> GetRecentHistoryAsync(int limit = 20)
    {
        var items = new List<InteractionHistoryItem>();

        const string sql = """
            SELECT
                id,
                accepted_substances_text,
                results_text,
                highest_severity,
                created_at
            FROM interaction_history
            ORDER BY id DESC
            LIMIT @limit;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await EnsureTableExistsAsync(connection);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 500));

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new InteractionHistoryItem
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                AcceptedSubstancesText = GetString(reader, "accepted_substances_text"),
                ResultsText = GetString(reader, "results_text"),
                HighestSeverity = GetNullableString(reader, "highest_severity"),
                CreatedAt = GetDateTime(reader, "created_at")
            });
        }

        return items;
    }

    private static async Task EnsureTableExistsAsync(SqliteConnection connection)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS interaction_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                accepted_substances_text TEXT NOT NULL,
                results_text TEXT NOT NULL,
                highest_severity TEXT NULL,
                created_at TEXT NOT NULL
            );
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static int GetSeverityScore(string severity)
    {
        var value = severity.Trim().ToLowerInvariant();

        return value switch
        {
            "contraindicated" => 5,
            "major" => 4,
            "moderate" => 3,
            "minor" => 2,
            "unknown" => 1,
            "x" => 5,
            "d" => 4,
            "c" => 3,
            "b" => 2,
            "a" => 1,
            _ => 0
        };
    }

    private static string GetString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static string? GetNullableString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime GetDateTime(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);

        if (reader.IsDBNull(ordinal))
        {
            return DateTime.MinValue;
        }

        return DateTime.TryParse(reader.GetString(ordinal), out var value)
            ? value
            : DateTime.MinValue;
    }
}
