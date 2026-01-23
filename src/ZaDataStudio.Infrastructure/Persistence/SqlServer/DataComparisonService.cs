using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Infrastructure.Persistence.SqlServer;

public class DataComparisonService
{
    public async Task<DataComparisonResult> CompareTableDataAsync(
        string sourceConnectionString,
        string destConnectionString,
        TableMapping mapping)
    {
        var result = new DataComparisonResult
        {
            SourceTable = mapping.SourceTable,
            DestinationTable = mapping.DestinationTable
        };

        try
        {
            // Detect if this is a lookup table
            result.IsLookupTable = await IsLookupTableAsync(sourceConnectionString, mapping.SourceTable);

            // Get column mappings or create default ones
            var columnMappings = mapping.ColumnMappings.Any() 
                ? mapping.ColumnMappings 
                : await GetDefaultColumnMappingsAsync(sourceConnectionString, destConnectionString, mapping);

            // Get key columns
            var keyColumns = columnMappings.Where(c => c.IsKey).ToList();
            if (!keyColumns.Any())
            {
                // Use primary keys if no explicit keys specified
                keyColumns = await GetPrimaryKeyColumnsAsync(sourceConnectionString, mapping.SourceTable, columnMappings);
            }

            if (!keyColumns.Any())
            {
                result.ErrorMessage = "No key columns found. Cannot compare data without a key.";
                return result;
            }

            // Compare data
            await CompareDataAsync(sourceConnectionString, destConnectionString, mapping, columnMappings, keyColumns, result);

            // Get distinct values for lookup tables
            if (result.IsLookupTable)
            {
                result.SourceDistinctValues = await GetDistinctValuesAsync(sourceConnectionString, mapping.SourceTable, columnMappings);
                result.DestinationDistinctValues = await GetDistinctValuesAsync(destConnectionString, mapping.DestinationTable, columnMappings);
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<bool> IsLookupTableAsync(string connectionString, string tableName)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var parts = tableName.Split('.');
        var schema = parts.Length > 1 ? parts[0] : "dbo";
        var table = parts.Length > 1 ? parts[1] : parts[0];

        // A table is considered a lookup if it has < 1000 rows and is referenced by foreign keys
        // First, get the row count
        var rowCountQuery = $"SELECT COUNT(*) FROM {tableName}";
        using var rowCountCommand = new SqlCommand(rowCountQuery, connection);
        var rowCount = (int?)await rowCountCommand.ExecuteScalarAsync() ?? 0;

        // Then get the FK count
        var fkQuery = @"
            SELECT COUNT(DISTINCT fk.name) as FKCount
            FROM sys.foreign_key_columns fkc
            INNER JOIN sys.foreign_keys fk ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.tables t ON fkc.referenced_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = @schema AND t.name = @table";

        using var fkCommand = new SqlCommand(fkQuery, connection);
        fkCommand.Parameters.AddWithValue("@schema", schema);
        fkCommand.Parameters.AddWithValue("@table", table);

        var fkCount = (int?)await fkCommand.ExecuteScalarAsync() ?? 0;

        return rowCount < 1000 && fkCount > 0;
    }

    private async Task<List<ColumnMapping>> GetDefaultColumnMappingsAsync(
        string sourceConnectionString,
        string destConnectionString,
        TableMapping tableMapping)
    {
        var mappings = new List<ColumnMapping>();

        using var sourceConn = new SqlConnection(sourceConnectionString);
        await sourceConn.OpenAsync();

        var query = @"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA + '.' + TABLE_NAME = @tableName
            ORDER BY ORDINAL_POSITION";

        using var command = new SqlCommand(query, sourceConn);
        command.Parameters.AddWithValue("@tableName", tableMapping.SourceTable);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var columnName = reader.GetString(0);
            mappings.Add(new ColumnMapping
            {
                SourceColumn = columnName,
                DestinationColumn = columnName,
                IsKey = false
            });
        }

        return mappings;
    }

    private async Task<List<ColumnMapping>> GetPrimaryKeyColumnsAsync(
        string connectionString,
        string tableName,
        List<ColumnMapping> allColumns)
    {
        var keyColumns = new List<ColumnMapping>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var parts = tableName.Split('.');
        var schema = parts.Length > 1 ? parts[0] : "dbo";
        var table = parts.Length > 1 ? parts[1] : parts[0];

        var query = @"
            SELECT ku.COLUMN_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                AND tc.TABLE_NAME = ku.TABLE_NAME
            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                AND tc.TABLE_SCHEMA = @schema
                AND tc.TABLE_NAME = @table
            ORDER BY ku.ORDINAL_POSITION";

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", table);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var columnName = reader.GetString(0);
            var mapping = allColumns.FirstOrDefault(c => c.SourceColumn == columnName);
            if (mapping != null)
            {
                mapping.IsKey = true;
                keyColumns.Add(mapping);
            }
        }

        return keyColumns;
    }

    private async Task CompareDataAsync(
        string sourceConnectionString,
        string destConnectionString,
        TableMapping tableMapping,
        List<ColumnMapping> columnMappings,
        List<ColumnMapping> keyColumns,
        DataComparisonResult result)
    {
        // Get source data
        var sourceData = await GetTableDataAsync(sourceConnectionString, tableMapping.SourceTable, columnMappings, true);
        var destData = await GetTableDataAsync(destConnectionString, tableMapping.DestinationTable, columnMappings, false);

        result.SourceRowCount = sourceData.Count;
        result.DestinationRowCount = destData.Count;

        // Compare rows
        foreach (var sourceRow in sourceData)
        {
            var key = GetRowKey(sourceRow, keyColumns, true);
            var destRow = destData.FirstOrDefault(d => GetRowKey(d, keyColumns, false) == key);

            if (destRow == null)
            {
                result.RowsOnlyInSource.Add(sourceRow);
            }
            else
            {
                var differences = CompareRows(sourceRow, destRow, columnMappings);
                if (differences.Any())
                {
                    result.RowsWithDifferences.Add(new RowDifference
                    {
                        Key = key,
                        SourceRow = sourceRow,
                        DestinationRow = destRow,
                        Differences = differences
                    });
                }
            }
        }

        // Find rows only in destination
        foreach (var destRow in destData)
        {
            var key = GetRowKey(destRow, keyColumns, false);
            var sourceRow = sourceData.FirstOrDefault(s => GetRowKey(s, keyColumns, true) == key);

            if (sourceRow == null)
            {
                result.RowsOnlyInDestination.Add(destRow);
            }
        }
    }

    private async Task<List<Dictionary<string, object?>>> GetTableDataAsync(
        string connectionString,
        string tableName,
        List<ColumnMapping> columnMappings,
        bool isSource)
    {
        var data = new List<Dictionary<string, object?>>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var columns = columnMappings.Select(c => isSource ? c.SourceColumn : c.DestinationColumn);
        var query = $"SELECT {string.Join(", ", columns.Select(c => $"[{c}]"))} FROM {tableName}";

        using var command = new SqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                row[columnName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            data.Add(row);
        }

        return data;
    }

    private string GetRowKey(Dictionary<string, object?> row, List<ColumnMapping> keyColumns, bool isSource)
    {
        var keyValues = keyColumns.Select(kc =>
        {
            var columnName = isSource ? kc.SourceColumn : kc.DestinationColumn;
            var value = row.ContainsKey(columnName) ? row[columnName] : null;
            return value?.ToString() ?? "NULL";
        });

        return string.Join("|", keyValues);
    }

    private List<string> CompareRows(
        Dictionary<string, object?> sourceRow,
        Dictionary<string, object?> destRow,
        List<ColumnMapping> columnMappings)
    {
        var differences = new List<string>();

        foreach (var mapping in columnMappings)
        {
            var sourceValue = sourceRow.ContainsKey(mapping.SourceColumn) ? sourceRow[mapping.SourceColumn] : null;
            var destValue = destRow.ContainsKey(mapping.DestinationColumn) ? destRow[mapping.DestinationColumn] : null;

            if (!AreValuesEqual(sourceValue, destValue))
            {
                differences.Add($"{mapping.SourceColumn}: {FormatValue(sourceValue)} → {FormatValue(destValue)}");
            }
        }

        return differences;
    }

    private bool AreValuesEqual(object? value1, object? value2)
    {
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;

        return value1.ToString() == value2.ToString();
    }

    private string FormatValue(object? value)
    {
        if (value == null) return "NULL";
        if (value is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss");
        if (value is string s) return $"'{s}'";
        return value.ToString() ?? "NULL";
    }

    private async Task<Dictionary<string, int>> GetDistinctValuesAsync(
        string connectionString,
        string tableName,
        List<ColumnMapping> columnMappings)
    {
        var distinctValues = new Dictionary<string, int>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var mapping in columnMappings.Take(5)) // Limit to first 5 columns
        {
            var query = $"SELECT COUNT(DISTINCT [{mapping.SourceColumn}]) FROM {tableName}";
            using var command = new SqlCommand(query, connection);
            var count = (int?)await command.ExecuteScalarAsync() ?? 0;
            distinctValues[mapping.SourceColumn] = count;
        }

        return distinctValues;
    }

    public string GenerateMigrationSQL(DataComparisonResult comparisonResult, TableMapping mapping)
    {
        var sql = new StringBuilder();
        sql.AppendLine($"-- Migration SQL for {mapping.SourceTable} → {mapping.DestinationTable}");
        sql.AppendLine($"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sql.AppendLine();

        var columnMappings = mapping.ColumnMappings.Any() ? mapping.ColumnMappings : new List<ColumnMapping>();

        // INSERT statements for rows only in source
        if (comparisonResult.RowsOnlyInSource.Any())
        {
            sql.AppendLine($"-- INSERT {comparisonResult.RowsOnlyInSource.Count} missing rows");
            sql.AppendLine();

            foreach (var row in comparisonResult.RowsOnlyInSource)
            {
                sql.AppendLine(GenerateInsertStatement(mapping.DestinationTable, row, columnMappings));
            }
            sql.AppendLine();
        }

        // UPDATE statements for rows with differences
        if (comparisonResult.RowsWithDifferences.Any())
        {
            sql.AppendLine($"-- UPDATE {comparisonResult.RowsWithDifferences.Count} rows with differences");
            sql.AppendLine();

            foreach (var diff in comparisonResult.RowsWithDifferences)
            {
                sql.AppendLine(GenerateUpdateStatement(mapping.DestinationTable, diff, columnMappings));
            }
            sql.AppendLine();
        }

        // DELETE statements for rows only in destination (commented out for safety)
        if (comparisonResult.RowsOnlyInDestination.Any())
        {
            sql.AppendLine($"-- DELETE {comparisonResult.RowsOnlyInDestination.Count} orphaned rows (commented for safety)");
            sql.AppendLine();

            foreach (var row in comparisonResult.RowsOnlyInDestination)
            {
                var deleteStmt = GenerateDeleteStatement(mapping.DestinationTable, row, columnMappings);
                sql.AppendLine($"-- {deleteStmt}");
            }
            sql.AppendLine();
        }

        if (!comparisonResult.HasDifferences)
        {
            sql.AppendLine("-- No data differences found. Tables are in sync.");
        }

        return sql.ToString();
    }

    private string GenerateInsertStatement(string tableName, Dictionary<string, object?> row, List<ColumnMapping> columnMappings)
    {
        var columns = row.Keys.ToList();
        var values = row.Values.Select(v => FormatValueForSQL(v)).ToList();

        return $"INSERT INTO {tableName} ({string.Join(", ", columns.Select(c => $"[{c}]"))}) VALUES ({string.Join(", ", values)});";
    }

    private string GenerateUpdateStatement(string tableName, RowDifference diff, List<ColumnMapping> columnMappings)
    {
        var keyColumns = columnMappings.Where(c => c.IsKey).ToList();
        var nonKeyColumns = columnMappings.Where(c => !c.IsKey).ToList();

        var setClauses = new List<string>();
        foreach (var col in nonKeyColumns)
        {
            var destColumn = col.DestinationColumn;
            if (diff.SourceRow.ContainsKey(col.SourceColumn))
            {
                var value = diff.SourceRow[col.SourceColumn];
                setClauses.Add($"[{destColumn}] = {FormatValueForSQL(value)}");
            }
        }

        var whereClauses = keyColumns.Select(kc =>
        {
            var value = diff.DestinationRow[kc.DestinationColumn];
            return $"[{kc.DestinationColumn}] = {FormatValueForSQL(value)}";
        });

        if (!setClauses.Any() || !whereClauses.Any())
            return $"-- Unable to generate UPDATE statement for key: {diff.Key}";

        return $"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)};";
    }

    private string GenerateDeleteStatement(string tableName, Dictionary<string, object?> row, List<ColumnMapping> columnMappings)
    {
        var keyColumns = columnMappings.Where(c => c.IsKey).ToList();
        
        if (!keyColumns.Any())
        {
            keyColumns = row.Keys.Take(1).Select(k => new ColumnMapping { DestinationColumn = k }).ToList();
        }

        var whereClauses = keyColumns.Select(kc =>
        {
            var value = row[kc.DestinationColumn];
            return $"[{kc.DestinationColumn}] = {FormatValueForSQL(value)}";
        });

        return $"DELETE FROM {tableName} WHERE {string.Join(" AND ", whereClauses)};";
    }

    private string FormatValueForSQL(object? value)
    {
        if (value == null) return "NULL";
        if (value is string s) return $"'{s.Replace("'", "''")}'";
        if (value is DateTime dt) return $"'{dt:yyyy-MM-dd HH:mm:ss}'";
        if (value is bool b) return b ? "1" : "0";
        return value.ToString() ?? "NULL";
    }
}

// DTOs for data comparison
public class DataComparisonResult
{
    public string SourceTable { get; set; } = string.Empty;
    public string DestinationTable { get; set; } = string.Empty;
    public int SourceRowCount { get; set; }
    public int DestinationRowCount { get; set; }
    public bool IsLookupTable { get; set; }
    public List<Dictionary<string, object?>> RowsOnlyInSource { get; set; } = new();
    public List<Dictionary<string, object?>> RowsOnlyInDestination { get; set; } = new();
    public List<RowDifference> RowsWithDifferences { get; set; } = new();
    public Dictionary<string, int> SourceDistinctValues { get; set; } = new();
    public Dictionary<string, int> DestinationDistinctValues { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;

    public bool HasDifferences =>
        RowsOnlyInSource.Any() ||
        RowsOnlyInDestination.Any() ||
        RowsWithDifferences.Any();
}

public class RowDifference
{
    public string Key { get; set; } = string.Empty;
    public Dictionary<string, object?> SourceRow { get; set; } = new();
    public Dictionary<string, object?> DestinationRow { get; set; } = new();
    public List<string> Differences { get; set; } = new();
}


