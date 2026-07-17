using DrugCompare.Application.Models.Rag;
using DrugCompare.Application.Services.Contracts.Rag;
using Microsoft.Data.Sqlite;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace DrugCompare.Infrastructure.SQLite.Rag;

public sealed class SqliteFtsRagRetriever : IRagRetriever
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteFtsRagRetriever(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<KnowledgeChunkResult>> RetrieveAsync(
        string query,
        RagRetrievalOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var allowedStatuses = BuildAllowedStatuses(options);

        if (allowedStatuses.Count == 0)
        {
            return [];
        }

        var sql = BuildSql(options, allowedStatuses);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        command.Parameters.AddWithValue("@query", BuildFtsQuery(query));
        command.Parameters.AddWithValue("@limit", Math.Clamp(options.Limit, 1, 50));

        for (var i = 0; i < allowedStatuses.Count; i++)
        {
            command.Parameters.AddWithValue($"@status{i}", allowedStatuses[i]);
        }

        if (!string.IsNullOrWhiteSpace(options.ProductName))
        {
            command.Parameters.AddWithValue("@product_name", options.ProductName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(options.SectionNumber))
        {
            command.Parameters.AddWithValue("@section_number", options.SectionNumber.Trim());
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<KnowledgeChunkResult>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(Map(reader));
        }

        return results;
    }

    private static string BuildSql(
        RagRetrievalOptions options,
        IReadOnlyList<string> allowedStatuses)
    {
        var builder = new StringBuilder();

        builder.AppendLine("""
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
                k.review_status,
                k.source_url
            FROM knowledge_chunks_fts
            JOIN knowledge_chunks k ON k.id = knowledge_chunks_fts.rowid
            WHERE knowledge_chunks_fts MATCH @query
        """);

        var statusPlaceholders = string.Join(
            ", ",
            allowedStatuses.Select((_, index) => $"@status{index}"));

        builder.AppendLine($"AND k.review_status IN ({statusPlaceholders})");

        if (!string.IsNullOrWhiteSpace(options.ProductName))
        {
            builder.AppendLine("AND LOWER(k.product_name) LIKE '%' || LOWER(@product_name) || '%'");
        }

        if (!string.IsNullOrWhiteSpace(options.SectionNumber))
        {
            builder.AppendLine("AND k.section_number = @section_number");
        }

        builder.AppendLine("""
            ORDER BY rank
            LIMIT @limit;
        """);

        return builder.ToString();
    }

    private static List<string> BuildAllowedStatuses(RagRetrievalOptions options)
    {
        var statuses = new List<string>();

        if (options.IncludeNeedsReview)
        {
            statuses.Add("needs_review");
        }

        if (options.IncludeReviewed)
        {
            statuses.Add("reviewed");
        }

        if (options.IncludeVerified)
        {
            statuses.Add("verified");
        }

        return statuses;
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

    private static KnowledgeChunkResult Map(SqliteDataReader reader)
    {
        return new KnowledgeChunkResult
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
            ReviewStatus = GetString(reader, "review_status"),
            SourceUrl = GetNullableString(reader, "source_url")
        };
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
}