namespace DrugCompare.Application.Services.Contracts;

public interface ILocalDatabaseBackupService
{
    Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default);

    Task<string> RestoreAsync(string sourcePath, CancellationToken cancellationToken = default);
}
