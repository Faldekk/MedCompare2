using DrugCompare.Application.Repositories.Contracts.KnowledgeBase;
using Microsoft.Data.Sqlite;

namespace DrugCompare.Infrastructure.SQLite.KnowledgeBase;

public sealed class SqliteKnowledgeBaseReviewRepository : IKnowledgeBaseReviewRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;
    public SqliteKnowledgeBaseReviewRepository(SqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task ReviewDocumentForChunkAsync(long chunkId, string newStatus, string reviewedBy, string? reviewNote, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var source = await GetSourceAsync(connection, transaction, chunkId, cancellationToken)
                ?? throw new InvalidOperationException("Nie znaleziono źródła dla wybranego chunku.");
            var now = DateTime.UtcNow.ToString("O");

            await UpdateAsync(connection, transaction, "UPDATE chpl_documents SET review_status=@status, reviewed_at=@at, reviewed_by=@by, review_note=@note WHERE id=@id;", source.DocumentId, newStatus, reviewedBy, reviewNote, now, cancellationToken);
            await UpdateAsync(connection, transaction, "UPDATE chpl_sections SET review_status=@status, reviewed_at=@at, reviewed_by=@by, review_note=@note WHERE chpl_document_id=@id;", source.DocumentId, newStatus, reviewedBy, reviewNote, now, cancellationToken);
            await UpdateAsync(connection, transaction, "UPDATE knowledge_chunks SET review_status=@status, reviewed_at=@at, reviewed_by=@by, review_note=@note WHERE source_type='ChPL' AND source_id IN (SELECT id FROM chpl_sections WHERE chpl_document_id=@id);", source.DocumentId, newStatus, reviewedBy, reviewNote, now, cancellationToken);

            await using var audit = connection.CreateCommand();
            audit.Transaction = transaction;
            audit.CommandText = "INSERT INTO audit_logs (event_type, details, created_at) VALUES ('ChplDocumentReviewed', @details, @at);";
            audit.Parameters.AddWithValue("@details", $"document_id={source.DocumentId}; previous_status={source.PreviousStatus}; new_status={newStatus}; reviewed_by={reviewedBy}; note={reviewNote ?? string.Empty}");
            audit.Parameters.AddWithValue("@at", now);
            await audit.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<(long DocumentId, string PreviousStatus)?> GetSourceAsync(SqliteConnection connection, SqliteTransaction transaction, long chunkId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT s.chpl_document_id, d.review_status FROM knowledge_chunks k JOIN chpl_sections s ON s.id=k.source_id JOIN chpl_documents d ON d.id=s.chpl_document_id WHERE k.id=@id AND k.source_type='ChPL' LIMIT 1;";
        command.Parameters.AddWithValue("@id", chunkId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? (reader.GetInt64(0), reader.GetString(1)) : null;
    }

    private static async Task UpdateAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, long documentId, string status, string reviewedBy, string? note, string at, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", documentId);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@by", reviewedBy);
        command.Parameters.AddWithValue("@note", string.IsNullOrWhiteSpace(note) ? DBNull.Value : note);
        command.Parameters.AddWithValue("@at", at);
        await command.ExecuteNonQueryAsync(ct);
    }
}
