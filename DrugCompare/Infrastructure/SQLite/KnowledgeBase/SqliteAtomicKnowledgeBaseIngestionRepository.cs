using System.Security.Cryptography;
using System.Text;
using DrugCompare.Application.Models.KnowledgeBase;
using DrugCompare.Application.Repositories.Contracts.KnowledgeBase;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;
using Microsoft.Data.Sqlite;

namespace DrugCompare.Infrastructure.SQLite.KnowledgeBase;

public sealed class SqliteAtomicKnowledgeBaseIngestionRepository : IAtomicKnowledgeBaseIngestionRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteAtomicKnowledgeBaseIngestionRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<KnowledgeBaseIngestionResult> IngestAsync(
        ChplDocumentRecord document,
        IReadOnlyCollection<ChplSectionRecord> sections,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var existingDocument = await GetByFileHashAsync(connection, transaction, document.FileHash, cancellationToken);
            if (existingDocument is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return new KnowledgeBaseIngestionResult
                {
                    ChplDocumentId = existingDocument.Id,
                    ReviewStatus = existingDocument.ReviewStatus,
                    WasAlreadyImported = true
                };
            }

            var documentId = await InsertDocumentAsync(connection, transaction, document, cancellationToken);
            var chunksSaved = 0;

            foreach (var section in sections)
            {
                var sectionId = await InsertSectionAsync(connection, transaction, documentId, section, cancellationToken);
                foreach (var chunk in CreateChunks(document, section, sectionId))
                {
                    chunksSaved += await InsertChunkAsync(connection, transaction, chunk, cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);

            return new KnowledgeBaseIngestionResult
            {
                ChplDocumentId = documentId,
                SectionsSaved = sections.Count,
                ChunksSaved = chunksSaved,
                ReviewStatus = document.ReviewStatus,
                WasAlreadyImported = false
            };
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<ChplDocumentRecord?> GetByFileHashAsync(SqliteConnection connection, SqliteTransaction transaction, string? fileHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileHash)) return null;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id, review_status FROM chpl_documents WHERE file_hash = @file_hash LIMIT 1;";
        command.Parameters.AddWithValue("@file_hash", fileHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ChplDocumentRecord { Id = reader.GetInt64(0), ReviewStatus = reader.GetString(1) }
            : null;
    }

    private static async Task<long> InsertDocumentAsync(SqliteConnection connection, SqliteTransaction transaction, ChplDocumentRecord document, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO chpl_documents (rpl_product_id, product_name, chpl_url, source_file, local_file_path, file_hash, document_type, language, parser_version, review_status, imported_at, parsed_at)
            VALUES (@rpl_product_id, @product_name, @chpl_url, @source_file, @local_file_path, @file_hash, @document_type, @language, @parser_version, @review_status, @imported_at, @parsed_at);
            SELECT last_insert_rowid();
            """;
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
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<long> InsertSectionAsync(SqliteConnection connection, SqliteTransaction transaction, long documentId, ChplSectionRecord section, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO chpl_sections (chpl_document_id, section_number, section_title, section_type, text, text_hash, review_status, created_at)
            VALUES (@chpl_document_id, @section_number, @section_title, @section_type, @text, @text_hash, @review_status, @created_at);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@chpl_document_id", documentId);
        command.Parameters.AddWithValue("@section_number", section.SectionNumber);
        command.Parameters.AddWithValue("@section_title", DbValue(section.SectionTitle));
        command.Parameters.AddWithValue("@section_type", DbValue(section.SectionType));
        command.Parameters.AddWithValue("@text", section.Text);
        command.Parameters.AddWithValue("@text_hash", DbValue(section.TextHash));
        command.Parameters.AddWithValue("@review_status", section.ReviewStatus);
        command.Parameters.AddWithValue("@created_at", ToDbDate(section.CreatedAt));
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> InsertChunkAsync(SqliteConnection connection, SqliteTransaction transaction, KnowledgeChunk chunk, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO knowledge_chunks (source_type, source_id, parent_section_id, chunk_index, source_title, product_name, active_substance, section_number, section_title, chunk_text, chunk_hash, review_status, clinical_category, clinical_priority, evidence_kind, source_url, created_at)
            VALUES (@source_type, @source_id, @parent_section_id, @chunk_index, @source_title, @product_name, @active_substance, @section_number, @section_title, @chunk_text, @chunk_hash, @review_status, @clinical_category, @clinical_priority, @evidence_kind, @source_url, @created_at);
            """;
        command.Parameters.AddWithValue("@source_type", chunk.SourceType);
        command.Parameters.AddWithValue("@source_id", chunk.SourceId!.Value);
        command.Parameters.AddWithValue("@parent_section_id", chunk.ParentSectionId!.Value);
        command.Parameters.AddWithValue("@chunk_index", chunk.ChunkIndex);
        command.Parameters.AddWithValue("@source_title", chunk.SourceTitle);
        command.Parameters.AddWithValue("@product_name", DbValue(chunk.ProductName));
        command.Parameters.AddWithValue("@active_substance", DbValue(chunk.ActiveSubstance));
        command.Parameters.AddWithValue("@section_number", DbValue(chunk.SectionNumber));
        command.Parameters.AddWithValue("@section_title", DbValue(chunk.SectionTitle));
        command.Parameters.AddWithValue("@chunk_text", chunk.ChunkText);
        command.Parameters.AddWithValue("@chunk_hash", chunk.ChunkHash);
        command.Parameters.AddWithValue("@review_status", chunk.ReviewStatus);
        command.Parameters.AddWithValue("@clinical_category", DbValue(chunk.ClinicalCategory));
        command.Parameters.AddWithValue("@clinical_priority", chunk.ClinicalPriority);
        command.Parameters.AddWithValue("@evidence_kind", chunk.EvidenceKind);
        command.Parameters.AddWithValue("@source_url", DbValue(chunk.SourceUrl));
        command.Parameters.AddWithValue("@created_at", ToDbDate(chunk.CreatedAt));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IReadOnlyList<KnowledgeChunk> CreateChunks(ChplDocumentRecord document, ChplSectionRecord section, long sectionId)
    {
        var productName = string.IsNullOrWhiteSpace(document.ProductName) ? "unknown product" : document.ProductName;
        var (category, priority) = GetClinicalMetadata(section.SectionNumber);
        return SplitSectionText(section.Text)
            .Select((text, index) => new KnowledgeChunk
        {
            SourceType = "ChPL", SourceId = sectionId, ParentSectionId = sectionId, ChunkIndex = index,
            SourceTitle = $"ChPL {productName}, section {section.SectionNumber}",
            ProductName = document.ProductName, ActiveSubstance = document.ActiveSubstanceText, SectionNumber = section.SectionNumber,
            SectionTitle = section.SectionTitle, ChunkText = text,
            ChunkHash = Sha256(string.Join("|", document.SourceFile, document.ProductName, section.SectionNumber, section.SectionTitle, index, text)),
            ReviewStatus = section.ReviewStatus, ClinicalCategory = category, ClinicalPriority = priority,
            EvidenceKind = "chpl_section", SourceUrl = document.ChplUrl, CreatedAt = section.CreatedAt
        })
            .ToList();
    }

    private static IReadOnlyList<string> SplitSectionText(string text)
    {
        const int targetLength = 1200;
        const int overlapLength = 150;
        var paragraphs = text.Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var paragraph in paragraphs.Length == 0 ? [text.Trim()] : paragraphs)
        {
            var remaining = paragraph.Trim();
            while (!string.IsNullOrWhiteSpace(remaining))
            {
                var separatorLength = current.Length > 0 ? 2 : 0;
                var availableLength = targetLength - current.Length - separatorLength;

                if (availableLength <= 0)
                {
                    chunks.Add(current.ToString());
                    var overlap = current.Length <= overlapLength ? current.ToString() : current.ToString()[^overlapLength..];
                    current.Clear();
                    current.Append(overlap.Trim());
                    continue;
                }

                if (remaining.Length <= availableLength)
                {
                    if (separatorLength > 0) current.AppendLine().AppendLine();
                    current.Append(remaining);
                    break;
                }

                var splitAt = remaining.LastIndexOf(' ', availableLength);
                if (splitAt < availableLength / 2)
                {
                    splitAt = availableLength;
                }

                if (separatorLength > 0) current.AppendLine().AppendLine();
                current.Append(remaining[..splitAt].Trim());
                chunks.Add(current.ToString());
                var nextOverlap = current.Length <= overlapLength ? current.ToString() : current.ToString()[^overlapLength..];
                current.Clear();
                current.Append(nextOverlap.Trim());
                remaining = remaining[splitAt..].TrimStart();
            }
        }

        if (current.Length > 0) chunks.Add(current.ToString());
        return chunks.Count == 0 ? [text] : chunks;
    }

    private static (string Category, int Priority) GetClinicalMetadata(string sectionNumber) => sectionNumber switch
    {
        "4.1" => ("indication", 80), "4.2" => ("posology", 90), "4.3" => ("contraindication", 100),
        "4.4" => ("warning", 100), "4.5" => ("interaction", 100), "4.6" => ("pregnancy_lactation_fertility", 95),
        "4.7" => ("driving_machines", 50), "4.8" => ("adverse_reaction", 90), "4.9" => ("overdose", 90),
        "5.1" => ("pharmacodynamics", 65), "5.2" => ("pharmacokinetics", 75), "5.3" => ("preclinical_safety", 40),
        "6.1" => ("excipient", 70), _ => ("unknown", 0)
    };

    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    private static object DbValue(long? value) => value ?? (object)DBNull.Value;
    private static object DbValue(DateTime? value) => value.HasValue ? ToDbDate(value.Value) : DBNull.Value;
    private static string ToDbDate(DateTime value) => value.ToUniversalTime().ToString("O");
    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
