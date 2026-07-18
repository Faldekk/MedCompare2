using DrugCompare.Infrastructure.SQLite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DrugCompare.Tests;

[TestClass]
public sealed class PolishDrugRegistryRepositoryTests
{
    [TestMethod]
    public async Task SearchAsync_FindsProductByPartialName()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"drugcompare-{Guid.NewGuid():N}.db");

        try
        {
            await CreateRegistryDatabaseAsync(databasePath);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}"
                })
                .Build();

            var repository = new SqlitePolishDrugRegistryRepository(
                new SqliteConnectionFactory(configuration));

            var results = await repository.SearchAsync("Apap");

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Apap Test", results[0].ProductName);
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static async Task CreateRegistryDatabaseAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE polish_drug_registry_items (
                id INTEGER PRIMARY KEY,
                rpl_id TEXT,
                product_name TEXT,
                normalized_product_name TEXT,
                active_substance_text TEXT,
                strength TEXT,
                pharmaceutical_form TEXT,
                marketing_authorization_holder TEXT,
                authorization_number TEXT,
                authorization_validity TEXT,
                product_type TEXT,
                procedure_type TEXT,
                chpl_url TEXT,
                leaflet_url TEXT,
                source TEXT,
                source_version TEXT,
                imported_at TEXT
            );

            INSERT INTO polish_drug_registry_items (
                id, product_name, normalized_product_name, active_substance_text, source, imported_at
            ) VALUES (1, 'Apap Test', 'apap test', 'Paracetamolum', 'RPL', '2026-01-01');
            """;
        await command.ExecuteNonQueryAsync();
    }
}
