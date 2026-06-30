using DrugCompare.Features.ChPLNavigator.Models;

namespace DrugCompare.Application.Services.Contracts.KnowledgeBase;

public interface IKnowledgeBaseIngestionService
{
    Task<KnowledgeBaseIngestionResult> IngestChplDocumentAsync(
        ChplDocument document,
        string? localFilePath = null,
        string? fileHash = null,
        CancellationToken cancellationToken = default);
}