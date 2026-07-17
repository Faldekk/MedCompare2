using DrugCompare.Application.Models;
using DrugCompare.Application.Repositories.Contracts;

namespace DrugCompare.Infrastructure.SQLite;

public sealed class SqliteAuditLogRepository : IAuditLogRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteAuditLogRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddAsync(string eventType, string? details = null)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            return;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS audit_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                event_type TEXT NOT NULL,
                details TEXT NULL,
                created_at TEXT NOT NULL
            );
            """;

        await using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText = createTableSql;
            await createCommand.ExecuteNonQueryAsync();
        }

        const string insertSql = """
            INSERT INTO audit_logs (event_type, details, created_at)
            VALUES (@event_type, @details, datetime('now'));
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = insertSql;
        command.Parameters.AddWithValue("@event_type", eventType.Trim());
        command.Parameters.AddWithValue("@details", (object?)details ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<AuditLogItem>> GetRecentLogsAsync(int limit = 50)
    {
        var result = new List<AuditLogItem>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS audit_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                event_type TEXT NOT NULL,
                details TEXT NULL,
                created_at TEXT NOT NULL
            );
            """;

        await using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText = createTableSql;
            await createCommand.ExecuteNonQueryAsync();
        }

        const string selectSql = """
            SELECT
                id,
                event_type,
                details,
                created_at
            FROM audit_logs
            ORDER BY created_at DESC
            LIMIT @limit;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = selectSql;
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 500));

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new AuditLogItem
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                EventType = reader.GetString(reader.GetOrdinal("event_type")),
                Details = reader.IsDBNull(reader.GetOrdinal("details"))
            ? null
            : reader.GetString(reader.GetOrdinal("details")),
                DetailsJson = reader.IsDBNull(reader.GetOrdinal("details"))
            ? null
            : reader.GetString(reader.GetOrdinal("details")),
                CreatedAt = DateTime.TryParse(reader.GetString(reader.GetOrdinal("created_at")), out var createdAt)
            ? createdAt
            : DateTime.MinValue
            });
        }

        return result;
    }
}
