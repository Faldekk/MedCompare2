using DrugCompare.Application.Models.KnowledgeBase;
using DrugCompare.Application.Repositories.Contracts.KnowledgeBase;
using Microsoft.Data.Sqlite;

namespace DrugCompare.Infrastructure.SQLite.KnowledgeBase;

public sealed class SqliteKnowledgeChunkRepository : IKnowledgeChunkRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteKnowledgeChunkRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> AddAsync(
        KnowledgeChunk chunk,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO knowledge_chunks (
                source_type,
                source_id,
                source_title,
                product_name,
                active_substance,
                section_number,
                section_title,
                chunk_text,
                chunk_hash,
                review_status,
                source_url,
                created_at
            )
            VALUES (
                @source_type,
                @source_id,
                @source_title,
                @product_name,
                @active_substance,
                @section_number,
                @section_title,
                @chunk_text,
                @chunk_hash,
                @review_status,
                @source_url,
                @created_at
            );

            SELECT last_insert_rowid();
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        AddParameters(command, chunk);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return Convert.ToInt64(result);
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<KnowledgeChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        const string sql = """
            INSERT OR IGNORE INTO knowledge_chunks (
                source_type,
                source_id,
                source_title,
                product_name,
                active_substance,
                section_number,
                section_title,
                chunk_text,
                chunk_hash,
                review_status,
                source_url,
                created_at
            )
            VALUES (
                @source_type,
                @source_id,
                @source_title,
                @product_name,
                @active_substance,
                @section_number,
                @section_title,
                @chunk_text,
                @chunk_hash,
                @review_status,
                @source_url,
                @created_at
            );
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var chunk in chunks)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = (SqliteTransaction)transaction;

            AddParameters(command, chunk);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<KnowledgeChunk?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                source_type,
                source_id,
                source_title,
                product_name,
                active_substance,
                section_number,
                section_title,
                chunk_text,
                chunk_hash,
                review_status,
                source_url,
                created_at
            FROM knowledge_chunks
            WHERE id = @id
            LIMIT 1;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<IReadOnlyList<KnowledgeChunk>> SearchFtsAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        const string sql = """
            SELECT
                k.id,
                k.source_type,
                k.source_id,
                k.source_title,
                k.product_name,
                k.active_substance,
                k.section_number,
                k.section_title,
                k.chunk_text,
                k.chunk_hash,
                k.review_status,
                k.source_url,
                k.created_at
            FROM knowledge_chunks_fts
            JOIN knowledge_chunks k ON k.id = knowledge_chunks_fts.rowid
            WHERE knowledge_chunks_fts MATCH @query
            ORDER BY rank
            LIMIT @limit;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@query", BuildFtsQuery(query));
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<KnowledgeChunk>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(Map(reader));
        }

        return results;
    }

    public async Task<IReadOnlyList<KnowledgeChunk>> GetBySourceAsync(
        string sourceType,
        long sourceId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                source_type,
                source_id,
                source_title,
                product_name,
                active_substance,
                section_number,
                section_title,
                chunk_text,
                chunk_hash,
                review_status,
                source_url,
                created_at
            FROM knowledge_chunks
            WHERE source_type = @source_type
              AND source_id = @source_id
            ORDER BY id;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@source_type", sourceType);
        command.Parameters.AddWithValue("@source_id", sourceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<KnowledgeChunk>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(Map(reader));
        }

        return results;
    }

    private static void AddParameters(SqliteCommand command, KnowledgeChunk chunk)
    {
        command.Parameters.AddWithValue("@source_type", chunk.SourceType);
        command.Parameters.AddWithValue("@source_id", DbValue(chunk.SourceId));
        command.Parameters.AddWithValue("@source_title", chunk.SourceTitle);
        command.Parameters.AddWithValue("@product_name", DbValue(chunk.ProductName));
        command.Parameters.AddWithValue("@active_substance", DbValue(chunk.ActiveSubstance));
        command.Parameters.AddWithValue("@section_number", DbValue(chunk.SectionNumber));
        command.Parameters.AddWithValue("@section_title", DbValue(chunk.SectionTitle));
        command.Parameters.AddWithValue("@chunk_text", chunk.ChunkText);
        command.Parameters.AddWithValue("@chunk_hash", chunk.ChunkHash);
        command.Parameters.AddWithValue("@review_status", chunk.ReviewStatus);
        command.Parameters.AddWithValue("@source_url", DbValue(chunk.SourceUrl));
        command.Parameters.AddWithValue("@created_at", ToDbDate(chunk.CreatedAt));
    }

    private static KnowledgeChunk Map(SqliteDataReader reader)
    {
        return new KnowledgeChunk
        {
            Id = GetInt64(reader, "id"),
            SourceType = GetString(reader, "source_type"),
            SourceId = GetNullableInt64(reader, "source_id"),
            SourceTitle = GetString(reader, "source_title"),
            ProductName = GetNullableString(reader, "product_name"),
            ActiveSubstance = GetNullableString(reader, "active_substance"),
            SectionNumber = GetNullableString(reader, "section_number"),
            SectionTitle = GetNullableString(reader, "section_title"),
            ChunkText = GetString(reader, "chunk_text"),
            ChunkHash = GetString(reader, "chunk_hash"),
            ReviewStatus = GetString(reader, "review_status"),
            SourceUrl = GetNullableString(reader, "source_url"),
            CreatedAt = GetDateTime(reader, "created_at")
        };
    }

    private static string BuildFtsQuery(string query)
    {
        var terms = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 2)
            .Select(term => $"\"{term.Replace("\"", "\"\"")}\"")
            .ToArray();

        return terms.Length == 0
            ? "\"\""
            : string.Join(" ", terms);
    }

    private static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? DBNull.Value
            : value;
    }

    private static object DbValue(long? value)
    {
        return value.HasValue
            ? value.Value
            : DBNull.Value;
    }

    private static string ToDbDate(DateTime value)
    {
        return value.ToUniversalTime().ToString("O");
    }

    private static long GetInt64(SqliteDataReader reader, string column)
    {
        return reader.GetInt64(reader.GetOrdinal(column));
    }

    private static long? GetNullableInt64(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
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