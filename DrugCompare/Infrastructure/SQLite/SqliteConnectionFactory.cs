using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace DrugCompare.Infrastructure.SQLite;

public class SqliteConnectionFactory
{
    private readonly string _connectionString;
    private readonly string _databasePath;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        var rawConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");

        var builder = new SqliteConnectionStringBuilder(rawConnectionString);

        var configuredDataSource = builder.DataSource;
        var bundledDatabasePath = Path.IsPathRooted(configuredDataSource)
            ? configuredDataSource
            : Path.Combine(AppContext.BaseDirectory, configuredDataSource);

        if (IsBundledDefaultDatabase(configuredDataSource))
        {
            var localDatabaseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClinicDaddy",
                "data");
            var localDatabasePath = Path.Combine(localDatabaseDirectory, "medcompare.db");

            if (!File.Exists(localDatabasePath))
            {
                if (!File.Exists(bundledDatabasePath))
                {
                    throw new FileNotFoundException("Nie znaleziono bazowego pliku SQLite.", bundledDatabasePath);
                }

                Directory.CreateDirectory(localDatabaseDirectory);
                File.Copy(bundledDatabasePath, localDatabasePath);
            }

            builder.DataSource = localDatabasePath;
        }
        else
        {
            builder.DataSource = bundledDatabasePath;
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource) || !File.Exists(builder.DataSource))
        {
            throw new FileNotFoundException(
                "Nie znaleziono lokalnej bazy danych SQLite.",
                builder.DataSource);
        }

        if (new FileInfo(builder.DataSource).Length < 1024)
        {
            throw new InvalidOperationException(
                $"Baza SQLite jest pusta lub uszkodzona: {builder.DataSource}");
        }

        // These settings are connection-scoped, unlike WAL mode configured by migrations.
        builder.ForeignKeys = true;
        builder.DefaultTimeout = 5;

        _connectionString = builder.ToString();
        _databasePath = builder.DataSource;
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public string DatabasePath => _databasePath;

    private static bool IsBundledDefaultDatabase(string configuredDataSource)
    {
        var normalized = configuredDataSource.Replace('/', Path.DirectorySeparatorChar).Trim();
        return normalized.Equals("data\\medcompare.db", StringComparison.OrdinalIgnoreCase);
    }
}
