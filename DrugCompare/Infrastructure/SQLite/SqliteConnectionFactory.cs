using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace DrugCompare.Infrastructure.SQLite;

public class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        var rawConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");

        var builder = new SqliteConnectionStringBuilder(rawConnectionString);

        if (!Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.Combine(AppContext.BaseDirectory, builder.DataSource);
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

        _connectionString = builder.ToString();
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}
