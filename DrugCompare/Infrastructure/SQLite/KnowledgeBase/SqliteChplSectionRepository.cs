using DrugCompare.Application.Models.KnowledgeBase;
using DrugCompare.Application.Repositories.Contracts.KnowledgeBase;
using Microsoft.Data.Sqlite;

namespace DrugCompare.Infrastructure.SQLite.KnowledgeBase;

public sealed class SqliteChplSectionRepository : IChplSectionRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteChplSectionRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> AddAsync(
        ChplSectionRecord section,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO chpl_sections (
                chpl_document_id,
                section_number,
                section_title,
                section_type,
                text,
                text_hash,
                review_status,
                created_at
            )
            VALUES (
                @chpl_document_id,
                @section_number,
                @section_title,
                @section_type,
                @text,
                @text_hash,
                @review_status,
                @created_at
            );

            SELECT last_insert_rowid();
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        AddParameters(command, section);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return Convert.ToInt64(result);
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<ChplSectionRecord> sections,
        CancellationToken cancellationToken = default)
    {
        if (sections.Count == 0)
        {
            return;
        }

        const string sql = """
            INSERT INTO chpl_sections (
                chpl_document_id,
                section_number,
                section_title,
                section_type,
                text,
                text_hash,
                review_status,
                created_at
            )
            VALUES (
                @chpl_document_id,
                @section_number,
                @section_title,
                @section_type,
                @text,
                @text_hash,
                @review_status,
                @created_at
            );
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var section in sections)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = (SqliteTransaction)transaction;

            AddParameters(command, section);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChplSectionRecord>> GetByDocumentIdAsync(
        long chplDocumentId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                chpl_document_id,
                section_number,
                section_title,
                section_type,
                text,
                text_hash,
                review_status,
                created_at
            FROM chpl_sections
            WHERE chpl_document_id = @chpl_document_id
            ORDER BY section_number;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@chpl_document_id", chplDocumentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<ChplSectionRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(Map(reader));
        }

        return results;
    }

    public async Task<IReadOnlyList<ChplSectionRecord>> GetBySectionNumberAsync(
        string sectionNumber,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                chpl_document_id,
                section_number,
                section_title,
                section_type,
                text,
                text_hash,
                review_status,
                created_at
            FROM chpl_sections
            WHERE section_number = @section_number
            ORDER BY created_at DESC
            LIMIT @limit;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@section_number", sectionNumber);
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<ChplSectionRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(Map(reader));
        }

        return results;
    }

    private static void AddParameters(SqliteCommand command, ChplSectionRecord section)
    {
        command.Parameters.AddWithValue("@chpl_document_id", section.ChplDocumentId);
        command.Parameters.AddWithValue("@section_number", section.SectionNumber);
        command.Parameters.AddWithValue("@section_title", DbValue(section.SectionTitle));
        command.Parameters.AddWithValue("@section_type", DbValue(section.SectionType));
        command.Parameters.AddWithValue("@text", section.Text);
        command.Parameters.AddWithValue("@text_hash", DbValue(section.TextHash));
        command.Parameters.AddWithValue("@review_status", section.ReviewStatus);
        command.Parameters.AddWithValue("@created_at", ToDbDate(section.CreatedAt));
    }

    private static ChplSectionRecord Map(SqliteDataReader reader)
    {
        return new ChplSectionRecord
        {
            Id = GetInt64(reader, "id"),
            ChplDocumentId = GetInt64(reader, "chpl_document_id"),
            SectionNumber = GetString(reader, "section_number"),
            SectionTitle = GetNullableString(reader, "section_title"),
            SectionType = GetNullableString(reader, "section_type"),
            Text = GetString(reader, "text"),
            TextHash = GetNullableString(reader, "text_hash"),
            ReviewStatus = GetString(reader, "review_status"),
            CreatedAt = GetDateTime(reader, "created_at")
        };
    }

    private static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? DBNull.Value
            : value;
    }

    private static string ToDbDate(DateTime value)
    {
        return value.ToUniversalTime().ToString("O");
    }

    private static long GetInt64(SqliteDataReader reader, string column)
    {
        return reader.GetInt64(reader.GetOrdinal(column));
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
        var value = GetString(reader, column);
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTime.MinValue;
    }
}