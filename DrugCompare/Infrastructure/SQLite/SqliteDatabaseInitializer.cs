using Microsoft.Data.Sqlite;
using System.IO;

namespace DrugCompare.Infrastructure.SQLite;

public sealed class SqliteDatabaseInitializer
{
    private static readonly (string Id, string RelativePath)[] Migrations =
    [
        ("001_knowledge_base", "database/schema_sqlite.sql"),
        ("002_knowledge_clinical_metadata", "database/migrations/002_knowledge_clinical_metadata.sql"),
        ("003_review_metadata", "database/migrations/003_review_metadata.sql"),
        ("004_section_aware_chunking", "database/migrations/004_section_aware_chunking.sql")
    ];

    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteDatabaseInitializer(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync()
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await ConfigureConnectionAsync(connection);
        await EnsureMigrationTableAsync(connection);

        foreach (var migration in Migrations)
        {
            if (await IsAppliedAsync(connection, migration.Id))
            {
                continue;
            }

            var migrationPath = Path.Combine(AppContext.BaseDirectory, migration.RelativePath);
            if (!File.Exists(migrationPath))
            {
                throw new FileNotFoundException("Nie znaleziono pliku migracji SQLite.", migrationPath);
            }

            var migrationSql = await File.ReadAllTextAsync(migrationPath);
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

            try
            {
                await using (var schemaCommand = connection.CreateCommand())
                {
                    schemaCommand.Transaction = transaction;
                    schemaCommand.CommandText = migrationSql;
                    await schemaCommand.ExecuteNonQueryAsync();
                }

                await using (var migrationCommand = connection.CreateCommand())
                {
                    migrationCommand.Transaction = transaction;
                    migrationCommand.CommandText = "INSERT INTO schema_migrations (migration_id, applied_at) VALUES (@migrationId, datetime('now'));";
                    migrationCommand.Parameters.AddWithValue("@migrationId", migration.Id);
                    await migrationCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }

    private static async Task ConfigureConnectionAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA busy_timeout = 5000;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureMigrationTableAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                migration_id TEXT PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> IsAppliedAsync(SqliteConnection connection, string migrationId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM schema_migrations WHERE migration_id = @migrationId);";
        command.Parameters.AddWithValue("@migrationId", migrationId);

        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1;
    }
}
