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

    private readonly IAtomicKnowledgeBaseIngestionRepository _ingestionRepository;

    public KnowledgeBaseIngestionService(
        IAtomicKnowledgeBaseIngestionRepository ingestionRepository)
    {
        _ingestionRepository = ingestionRepository;
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

        var now = DateTime.UtcNow;

        var documentRecord = new ChplDocumentRecord
        {
            RplProductId = document.RplProductId,
            ProductName = document.ProductName,
            ActiveSubstanceText = document.ActiveSubstanceText,
            ChplUrl = document.ChplUrl,
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

        var sectionRecords = document.Sections
            .Where(section => !string.IsNullOrWhiteSpace(section.Text))
            .Select(section => new ChplSectionRecord
            {
                SectionNumber = section.SectionNumber,
                SectionTitle = section.Title,
                SectionType = ResolveSectionType(section.SectionNumber),
                Text = section.Text,
                TextHash = ComputeSha256(section.Text),
                ReviewStatus = DefaultReviewStatus,
                CreatedAt = now
            })
            .ToList();

        if (sectionRecords.Count == 0)
        {
            throw new InvalidOperationException("Cannot ingest ChPL document without non-empty parsed sections.");
        }

        return await _ingestionRepository.IngestAsync(documentRecord, sectionRecords, cancellationToken);
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

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
