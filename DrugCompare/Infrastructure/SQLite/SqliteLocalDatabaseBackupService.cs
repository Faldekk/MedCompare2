using DrugCompare.Application.Services.Contracts;
using Microsoft.Data.Sqlite;
using System.IO;

namespace DrugCompare.Infrastructure.SQLite;

public sealed class SqliteLocalDatabaseBackupService : ILocalDatabaseBackupService
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteLocalDatabaseBackupService(SqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("Wybierz plik kopii zapasowej.", nameof(destinationPath));
        var fullPath = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        if (File.Exists(fullPath)) File.Delete(fullPath);

        await using var source = _connectionFactory.CreateConnection();
        await source.OpenAsync(cancellationToken);
        await using var destination = new SqliteConnection($"Data Source={fullPath};Mode=ReadWriteCreate");
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    public async Task<string> RestoreAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath)) throw new FileNotFoundException("Nie znaleziono wybranej kopii bazy.", fullSourcePath);
        if (string.Equals(fullSourcePath, _connectionFactory.DatabasePath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Nie można przywrócić bazy z tego samego pliku.");

        await ValidateDatabaseAsync(fullSourcePath, cancellationToken);
        var automaticBackupPath = Path.Combine(Path.GetDirectoryName(_connectionFactory.DatabasePath)!, $"medcompare-before-restore-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db");
        await CreateBackupAsync(automaticBackupPath, cancellationToken);

        SqliteConnection.ClearAllPools();
        File.Copy(fullSourcePath, _connectionFactory.DatabasePath, overwrite: true);
        return automaticBackupPath;
    }

    private static async Task ValidateDatabaseAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Wybrany plik nie przeszedł kontroli integralności SQLite.");
    }
}
