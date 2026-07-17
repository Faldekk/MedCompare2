using DrugCompare.Application.Models;
using DrugCompare.Application.Repositories.Contracts;

namespace DrugCompare.Infrastructure.SQLite;

public sealed class SqlitePolishDrugRegistryRepository : IPolishDrugRegistryRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqlitePolishDrugRegistryRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<PolishDrugRegistryItem>> SearchAsync(string query, int limit = 100)
    {
        var result = new List<PolishDrugRegistryItem>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var sql = """
            SELECT
                id,
                rpl_id,
                product_name,
                normalized_product_name,
                active_substance_text,
                strength,
                pharmaceutical_form,
                marketing_authorization_holder,
                authorization_number,
                authorization_validity,
                product_type,
                procedure_type,
                chpl_url,
                leaflet_url,
                source,
                source_version,
                imported_at
            FROM polish_drug_registry_items
            WHERE 1 = 1
            """;

        if (!string.IsNullOrWhiteSpace(query))
        {
            sql += Environment.NewLine;
            sql += """
                AND (
                    product_name LIKE @query COLLATE NOCASE
                    OR normalized_product_name LIKE @query COLLATE NOCASE
                    OR active_substance_text LIKE @query COLLATE NOCASE
                    OR marketing_authorization_holder LIKE @query COLLATE NOCASE
                    OR authorization_number LIKE @query COLLATE NOCASE
                    OR rpl_id LIKE @query COLLATE NOCASE
                    OR strength LIKE @query COLLATE NOCASE
                    OR pharmaceutical_form LIKE @query COLLATE NOCASE
                )
                """;
        }

        sql += """
            ORDER BY
                CASE
                    WHEN product_name = @exactQuery COLLATE NOCASE THEN 0
                    WHEN product_name LIKE @startsWithQuery COLLATE NOCASE THEN 1
                    WHEN normalized_product_name LIKE @startsWithQuery COLLATE NOCASE THEN 2
                    WHEN active_substance_text LIKE @startsWithQuery COLLATE NOCASE THEN 3
                    ELSE 4
                END,
                product_name
            LIMIT @limit;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var trimmedQuery = query?.Trim() ?? string.Empty;

        command.Parameters.AddWithValue("@query", $"%{trimmedQuery}%");
        command.Parameters.AddWithValue("@exactQuery", trimmedQuery);
        command.Parameters.AddWithValue("@startsWithQuery", $"{trimmedQuery}%");
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 500));

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new PolishDrugRegistryItem
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                RplId = ReadNullableString(reader, "rpl_id"),

                ProductName = ReadNullableString(reader, "product_name") ?? string.Empty,
                NormalizedProductName = ReadNullableString(reader, "normalized_product_name") ?? string.Empty,

                ActiveSubstanceText = ReadNullableString(reader, "active_substance_text"),
                Strength = ReadNullableString(reader, "strength"),
                PharmaceuticalForm = ReadNullableString(reader, "pharmaceutical_form"),
                MarketingAuthorizationHolder = ReadNullableString(reader, "marketing_authorization_holder"),

                AuthorizationNumber = ReadNullableString(reader, "authorization_number"),
                AuthorizationValidity = ReadNullableString(reader, "authorization_validity"),
                ProductType = ReadNullableString(reader, "product_type"),
                ProcedureType = ReadNullableString(reader, "procedure_type"),

                ChplUrl = ReadNullableString(reader, "chpl_url"),
                LeafletUrl = ReadNullableString(reader, "leaflet_url"),

                Source = ReadNullableString(reader, "source") ?? "RPL",
                SourceVersion = ReadNullableString(reader, "source_version"),

                ImportedAt = ReadNullableDateTime(reader, "imported_at")
            });
        }

        return result;
    }

    private static string? ReadNullableString(System.Data.Common.DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetString(ordinal);
    }

    private static DateTime ReadNullableDateTime(System.Data.Common.DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            return default;
        }

        var rawValue = reader.GetValue(ordinal);

        if (rawValue is DateTime dateTime)
        {
            return dateTime;
        }

        return DateTime.TryParse(rawValue.ToString(), out var parsed)
            ? parsed
            : default;
    }
}
