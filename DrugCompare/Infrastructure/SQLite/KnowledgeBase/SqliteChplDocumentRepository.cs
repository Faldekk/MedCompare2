using DrugCompare.Application.Models.KnowledgeBase;
using DrugCompare.Application.Repositories.Contracts.KnowledgeBase;
using Microsoft.Data.Sqlite;

namespace DrugCompare.Infrastructure.SQLite.KnowledgeBase;

public sealed class SqliteChplDocumentRepository : IChplDocumentRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteChplDocumentRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> AddAsync(
        ChplDocumentRecord document,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO chpl_documents (
                rpl_product_id,
                product_name,
                chpl_url,
                source_file,
                local_file_path,
                file_hash,
                document_type,
                language,
                parser_version,
                review_status,
                imported_at,
                parsed_at
            )
            VALUES (
                @rpl_product_id,
                @product_name,
                @chpl_url,
                @source_file,
                @local_file_path,
                @file_hash,
                @document_type,
                @language,
                @parser_version,
                @review_status,
                @imported_at,
                @parsed_at
            );

            SELECT last_insert_rowid();
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        command.Parameters.AddWithValue("@rpl_product_id", DbValue(document.RplProductId));
        command.Parameters.AddWithValue("@product_name", DbValue(document.ProductName));
        command.Parameters.AddWithValue("@chpl_url", DbValue(document.ChplUrl));
        command.Parameters.AddWithValue("@source_file", DbValue(document.SourceFile));
        command.Parameters.AddWithValue("@local_file_path", DbValue(document.LocalFilePath));
        command.Parameters.AddWithValue("@file_hash", DbValue(document.FileHash));
        command.Parameters.AddWithValue("@document_type", document.DocumentType);
        command.Parameters.AddWithValue("@language", document.Language);
        command.Parameters.AddWithValue("@parser_version", DbValue(document.ParserVersion));
        command.Parameters.AddWithValue("@review_status", document.ReviewStatus);
        command.Parameters.AddWithValue("@imported_at", ToDbDate(document.ImportedAt));
        command.Parameters.AddWithValue("@parsed_at", DbValue(document.ParsedAt));

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return Convert.ToInt64(result);
    }

    public async Task<ChplDocumentRecord?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                rpl_product_id,
                product_name,
                chpl_url,
                source_file,
                local_file_path,
                file_hash,
                document_type,
                language,
                parser_version,
                review_status,
                imported_at,
                parsed_at
            FROM chpl_documents
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

    public async Task<ChplDocumentRecord?> GetByFileHashAsync(
        string fileHash,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                rpl_product_id,
                product_name,
                chpl_url,
                source_file,
                local_file_path,
                file_hash,
                document_type,
                language,
                parser_version,
                review_status,
                imported_at,
                parsed_at
            FROM chpl_documents
            WHERE file_hash = @file_hash
            LIMIT 1;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@file_hash", fileHash);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<IReadOnlyList<ChplDocumentRecord>> SearchByProductNameAsync(
        string productName,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return [];
        }

        const string sql = """
            SELECT
                id,
                rpl_product_id,
                product_name,
                chpl_url,
                source_file,
                local_file_path,
                file_hash,
                document_type,
                language,
                parser_version,
                review_status,
                imported_at,
                parsed_at
            FROM chpl_documents
            WHERE LOWER(product_name) LIKE '%' || LOWER(@product_name) || '%'
            ORDER BY imported_at DESC
            LIMIT @limit;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@product_name", productName.Trim());
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<ChplDocumentRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(Map(reader));
        }

        return results;
    }

    private static ChplDocumentRecord Map(SqliteDataReader reader)
    {
        return new ChplDocumentRecord
        {
            Id = GetInt64(reader, "id"),
            RplProductId = GetNullableInt64(reader, "rpl_product_id"),
            ProductName = GetNullableString(reader, "product_name"),
            ChplUrl = GetNullableString(reader, "chpl_url"),
            SourceFile = GetNullableString(reader, "source_file"),
            LocalFilePath = GetNullableString(reader, "local_file_path"),
            FileHash = GetNullableString(reader, "file_hash"),
            DocumentType = GetString(reader, "document_type"),
            Language = GetString(reader, "language"),
            ParserVersion = GetNullableString(reader, "parser_version"),
            ReviewStatus = GetString(reader, "review_status"),
            ImportedAt = GetDateTime(reader, "imported_at"),
            ParsedAt = GetNullableDateTime(reader, "parsed_at")
        };
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

    private static object DbValue(DateTime? value)
    {
        return value.HasValue
            ? ToDbDate(value.Value)
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

    private static DateTime? GetNullableDateTime(SqliteDataReader reader, string column)
    {
        var value = GetNullableString(reader, column);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }
}