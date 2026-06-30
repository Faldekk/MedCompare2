using System.Security.Cryptography;
using System.Text;
using DrugCompare.Application.Models.KnowledgeBase;
using DrugCompare.Application.Repositories.Contracts.KnowledgeBase;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;
using DrugCompare.Features.ChPLNavigator.Models;

namespace DrugCompare.Application.Services.Implementations.KnowledgeBase;

public sealed class KnowledgeBaseIngestionService : IKnowledgeBaseIngestionService
{
    private const string ParserVersion = "chpl-parser-v1";
    private const string DefaultReviewStatus = "needs_review";

    private readonly IChplDocumentRepository _documentRepository;
    private readonly IChplSectionRepository _sectionRepository;
    private readonly IKnowledgeChunkRepository _chunkRepository;

    public KnowledgeBaseIngestionService(
        IChplDocumentRepository documentRepository,
        IChplSectionRepository sectionRepository,
        IKnowledgeChunkRepository chunkRepository)
    {
        _documentRepository = documentRepository;
        _sectionRepository = sectionRepository;
        _chunkRepository = chunkRepository;
    }

    public async Task<KnowledgeBaseIngestionResult> IngestChplDocumentAsync(
        ChplDocument document,
        string? localFilePath = null,
        string? fileHash = null,
        CancellationToken cancellationToken = default)
    {
        if (document.Sections.Count == 0)
        {
            throw new InvalidOperationException("Cannot ingest ChPL document without parsed sections.");
        }

        if (!string.IsNullOrWhiteSpace(fileHash))
        {
            var existingDocument = await _documentRepository.GetByFileHashAsync(
                fileHash,
                cancellationToken);

            if (existingDocument is not null)
            {
                return new KnowledgeBaseIngestionResult
                {
                    ChplDocumentId = existingDocument.Id,
                    SectionsSaved = 0,
                    ChunksSaved = 0,
                    ReviewStatus = existingDocument.ReviewStatus,
                    WasAlreadyImported = true
                };
            }
        }

        var now = DateTime.UtcNow;

        var documentRecord = new ChplDocumentRecord
        {
            ProductName = document.ProductName,
            SourceFile = document.SourceFile,
            LocalFilePath = localFilePath,
            FileHash = fileHash,
            DocumentType = document.DocumentType,
            Language = document.Language,
            ParserVersion = ParserVersion,
            ReviewStatus = DefaultReviewStatus,
            ImportedAt = now,
            ParsedAt = document.ParsedAt
        };

        var documentId = await _documentRepository.AddAsync(
            documentRecord,
            cancellationToken);

        var sectionRecords = document.Sections
            .Where(section => !string.IsNullOrWhiteSpace(section.Text))
            .Select(section => new ChplSectionRecord
            {
                ChplDocumentId = documentId,
                SectionNumber = section.SectionNumber,
                SectionTitle = section.Title,
                SectionType = ResolveSectionType(section.SectionNumber),
                Text = section.Text,
                TextHash = ComputeSha256(section.Text),
                ReviewStatus = DefaultReviewStatus,
                CreatedAt = now
            })
            .ToList();

        await _sectionRepository.AddRangeAsync(
            sectionRecords,
            cancellationToken);

        var savedSections = await _sectionRepository.GetByDocumentIdAsync(
            documentId,
            cancellationToken);

        var chunks = savedSections
            .Select(section => new KnowledgeChunk
            {
                SourceType = "ChPL",
                SourceId = section.Id,
                SourceTitle = BuildSourceTitle(document, section),
                ProductName = document.ProductName,
                ActiveSubstance = null,
                SectionNumber = section.SectionNumber,
                SectionTitle = section.SectionTitle,
                ChunkText = section.Text,
                ChunkHash = ComputeChunkHash(document, section),
                ReviewStatus = DefaultReviewStatus,
                SourceUrl = null,
                CreatedAt = now
            })
            .ToList();

        await _chunkRepository.AddRangeAsync(
            chunks,
            cancellationToken);

        return new KnowledgeBaseIngestionResult
        {
            ChplDocumentId = documentId,
            SectionsSaved = sectionRecords.Count,
            ChunksSaved = chunks.Count,
            ReviewStatus = DefaultReviewStatus,
            WasAlreadyImported = false
        };
    }

    private static string BuildSourceTitle(
        ChplDocument document,
        ChplSectionRecord section)
    {
        var productName = string.IsNullOrWhiteSpace(document.ProductName)
            ? "unknown product"
            : document.ProductName;

        return $"ChPL {productName}, section {section.SectionNumber}";
    }

    private static string ResolveSectionType(string sectionNumber)
    {
        return sectionNumber switch
        {
            "4.1" => "therapeutic_indications",
            "4.2" => "posology",
            "4.3" => "contraindication",
            "4.4" => "warning",
            "4.5" => "interaction",
            "4.6" => "pregnancy_lactation_fertility",
            "4.7" => "driving_machines",
            "4.8" => "adverse_reaction",
            "4.9" => "overdose",
            "5.1" => "pharmacodynamics",
            "5.2" => "pharmacokinetics",
            "5.3" => "preclinical_safety",
            "6.1" => "excipients",
            _ => "unknown"
        };
    }

    private static string ComputeChunkHash(
        ChplDocument document,
        ChplSectionRecord section)
    {
        var input = string.Join(
            "|",
            document.SourceFile,
            document.ProductName,
            section.SectionNumber,
            section.SectionTitle,
            section.TextHash);

        return ComputeSha256(input);
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}