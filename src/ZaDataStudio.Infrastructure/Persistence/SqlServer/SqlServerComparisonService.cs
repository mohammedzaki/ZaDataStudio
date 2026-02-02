using Microsoft.Data.SqlClient;
using System.Data;
using ZaDataStudio.Application.Common.Interfaces;
using ZaDataStudio.Domain.Entities;
using DomainTableSchema = ZaDataStudio.Domain.Entities.TableSchema;
using DomainColumnSchema = ZaDataStudio.Domain.Entities.ColumnSchema;
using DomainColumnTypeInfo = ZaDataStudio.Domain.Entities.ColumnTypeInfo;
using DomainComparisonResult = ZaDataStudio.Domain.Entities.ComparisonResult;
using DomainTableDifference = ZaDataStudio.Domain.Entities.TableDifference;
using DomainColumnTypeComparisonResult = ZaDataStudio.Domain.Entities.ColumnTypeComparisonResult;
using DomainColumnTypeDifference = ZaDataStudio.Domain.Entities.ColumnTypeDifference;

namespace ZaDataStudio.Infrastructure.Persistence.SqlServer;

public class SqlServerComparisonService
{
    private readonly SqlServerDatabaseService _databaseService;

    public SqlServerComparisonService(SqlServerDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(string connectionString)
    {
        var result = new ConnectionTestResult();

        try
        {
            var testResult = await _databaseService.TestConnectionAsync(connectionString);

            result.IsSuccessful = testResult.IsSuccessful;
            result.ServerName = testResult.ServerName ?? "Unknown";
            result.DatabaseName = testResult.DatabaseName ?? "Unknown";
            result.ResponseTime = testResult.ResponseTime;
            result.ErrorMessage = testResult.ErrorMessage;

            // Get version if successful
            if (testResult.IsSuccessful)
            {
                var versionQuery = "SELECT @@VERSION as Version";
                var versionResult = await _databaseService.ExecuteQueryAsync(connectionString, versionQuery);
                result.Version = versionResult.FirstOrDefault()?["Version"]?.ToString() ?? "Unknown";
            }
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<List<string>> GetTableNamesAsync(string connectionString)
    {
        try
        {
            return await _databaseService.GetTableNamesAsync(connectionString);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving table names: {ex.Message}", ex);
        }
    }

    public async Task<List<DomainColumnTypeInfo>> GetColumnTypesAsync(string connectionString, string tableName)
    {
        var columns = new List<DomainColumnTypeInfo>();

        try
        {
            var columnTypes = await _databaseService.GetColumnTypesAsync(connectionString, tableName);

            foreach (var kvp in columnTypes)
            {
                var info = kvp.Value;
                columns.Add(new DomainColumnTypeInfo
                {
                    ColumnName = info.ColumnName,
                    DataType = info.DataType,
                    MaxLength = info.MaxLength,
                    NumericPrecision = info.Precision.HasValue ? (byte?)info.Precision.Value : null,
                    NumericScale = info.Scale,
                    IsNullable = info.IsNullable
                });
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving column types: {ex.Message}", ex);
        }

        return columns;
    }

    public DomainColumnTypeComparisonResult CompareColumnTypes(
        List<DomainColumnTypeInfo> sourceColumns,
        List<DomainColumnTypeInfo> destColumns,
        List<ColumnMapping> mappings)
    {
        var result = new DomainColumnTypeComparisonResult();

        foreach (var mapping in mappings.Where(m => !string.IsNullOrEmpty(m.SourceColumn) && !string.IsNullOrEmpty(m.DestinationColumn)))
        {
            var sourceCol = sourceColumns.FirstOrDefault(c => c.ColumnName == mapping.SourceColumn);
            var destCol = destColumns.FirstOrDefault(c => c.ColumnName == mapping.DestinationColumn);

            if (sourceCol == null || destCol == null)
                continue;

            var differences = new List<string>();

            if (sourceCol.DataType != destCol.DataType)
                differences.Add($"DataType: {sourceCol.DataType} vs {destCol.DataType}");

            if (sourceCol.MaxLength != destCol.MaxLength)
                differences.Add($"MaxLength: {sourceCol.MaxLength} vs {destCol.MaxLength}");

            if (sourceCol.NumericPrecision != destCol.NumericPrecision)
                differences.Add($"Precision: {sourceCol.NumericPrecision} vs {destCol.NumericPrecision}");

            if (sourceCol.NumericScale != destCol.NumericScale)
                differences.Add($"Scale: {sourceCol.NumericScale} vs {destCol.NumericScale}");

            if (sourceCol.IsNullable != destCol.IsNullable)
                differences.Add($"Nullable: {sourceCol.IsNullable} vs {destCol.IsNullable}");

            if (differences.Any())
            {
                result.Differences.Add(new DomainColumnTypeDifference
                {
                    SourceColumn = mapping.SourceColumn,
                    DestinationColumn = mapping.DestinationColumn,
                    SourceType = sourceCol.DisplayType,
                    DestinationType = destCol.DisplayType,
                    DifferenceDetails = differences
                });
            }
        }

        return result;
    }

    public async Task<List<DomainTableSchema>> GetTableSchemasAsync(string connectionString)
    {
        var tables = new List<DomainTableSchema>();

        try
        {
            var connection = await _databaseService._connectionManager.GetConnectionAsync(connectionString);

            var query = @"
                SELECT 
                    t.TABLE_SCHEMA,
                    t.TABLE_NAME,
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.NUMERIC_PRECISION,
                    c.NUMERIC_SCALE,
                    c.IS_NULLABLE,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY
                FROM INFORMATION_SCHEMA.TABLES t
                LEFT JOIN INFORMATION_SCHEMA.COLUMNS c 
                    ON t.TABLE_SCHEMA = c.TABLE_SCHEMA 
                    AND t.TABLE_NAME = c.TABLE_NAME
                LEFT JOIN (
                    SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                        ON tc.CONSTRAINT_TYPE = 'PRIMARY KEY' 
                        AND tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                        AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                        AND tc.TABLE_NAME = ku.TABLE_NAME
                ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
                    AND c.TABLE_NAME = pk.TABLE_NAME 
                    AND c.COLUMN_NAME = pk.COLUMN_NAME
                WHERE t.TABLE_TYPE = 'BASE TABLE'
                ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION";

            using var command = connection.CreateCommand();
            command.CommandText = query;
            using var reader = await command.ExecuteReaderAsync();

            DomainTableSchema? currentTable = null;

            while (await reader.ReadAsync())
            {
                var schema = reader.GetString(0);
                var tableName = reader.GetString(1);
                var fullTableName = $"{schema}.{tableName}";

                if (currentTable == null || currentTable.FullName != fullTableName)
                {
                    currentTable = new DomainTableSchema
                    {
                        Schema = schema,
                        TableName = tableName,
                        FullName = fullTableName,
                        Columns = new List<DomainColumnSchema>()
                    };
                    tables.Add(currentTable);
                }

                if (!reader.IsDBNull(2))
                {
                    var column = new DomainColumnSchema
                    {
                        ColumnName = reader.GetString(2),
                        DataType = reader.GetString(3),
                        MaxLength = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                        NumericPrecision = reader.IsDBNull(5) ? null : reader.GetByte(5),
                        NumericScale = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                        IsNullable = reader.GetString(7) == "YES",
                        IsPrimaryKey = reader.GetInt32(8) == 1
                    };

                    currentTable.Columns.Add(column);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving table schemas: {ex.Message}", ex);
        }

        return tables;
    }

    public async Task<List<DomainTableSchema>> GetTableSchemasAsync(string connectionString, List<string> tableNames)
    {
        var tables = new List<DomainTableSchema>();

        if (tableNames == null || !tableNames.Any())
            return tables;

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Build the WHERE clause for specific tables
            var tableConditions = tableNames.Select((_, index) => 
                $"(t.TABLE_SCHEMA + '.' + t.TABLE_NAME = @table{index})").ToList();
            var whereClause = string.Join(" OR ", tableConditions);

            var query = $@"
                SELECT 
                    t.TABLE_SCHEMA,
                    t.TABLE_NAME,
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.NUMERIC_PRECISION,
                    c.NUMERIC_SCALE,
                    c.IS_NULLABLE,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY
                FROM INFORMATION_SCHEMA.TABLES t
                LEFT JOIN INFORMATION_SCHEMA.COLUMNS c 
                    ON t.TABLE_SCHEMA = c.TABLE_SCHEMA 
                    AND t.TABLE_NAME = c.TABLE_NAME
                LEFT JOIN (
                    SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                        ON tc.CONSTRAINT_TYPE = 'PRIMARY KEY' 
                        AND tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                        AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                        AND tc.TABLE_NAME = ku.TABLE_NAME
                ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
                    AND c.TABLE_NAME = pk.TABLE_NAME 
                    AND c.COLUMN_NAME = pk.COLUMN_NAME
                WHERE t.TABLE_TYPE = 'BASE TABLE' AND ({whereClause})
                ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION";

            using var command = new SqlCommand(query, connection);
            
            // Add parameters for each table name
            for (int i = 0; i < tableNames.Count; i++)
            {
                command.Parameters.AddWithValue($"@table{i}", tableNames[i]);
            }

            using var reader = await command.ExecuteReaderAsync();

            DomainTableSchema? currentTable = null;

            while (await reader.ReadAsync())
            {
                var schema = reader.GetString(0);
                var tableName = reader.GetString(1);
                var fullTableName = $"{schema}.{tableName}";

                if (currentTable == null || currentTable.FullName != fullTableName)
                {
                    currentTable = new DomainTableSchema
                    {
                        Schema = schema,
                        TableName = tableName,
                        FullName = fullTableName,
                        Columns = new List<DomainColumnSchema>()
                    };
                    tables.Add(currentTable);
                }

                if (!reader.IsDBNull(2))
                {
                    var column = new DomainColumnSchema
                    {
                        ColumnName = reader.GetString(2),
                        DataType = reader.GetString(3),
                        MaxLength = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                        NumericPrecision = reader.IsDBNull(5) ? null : reader.GetByte(5),
                        NumericScale = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                        IsNullable = reader.GetString(7) == "YES",
                        IsPrimaryKey = reader.GetInt32(8) == 1
                    };

                    currentTable.Columns.Add(column);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving table schemas: {ex.Message}", ex);
        }

        return tables;
    }

    public DomainComparisonResult CompareSchemas(List<DomainTableSchema> sourceTables, List<DomainTableSchema> destinationTables)
    {
        var result = new DomainComparisonResult();

        var sourceTableDict = sourceTables.ToDictionary(t => t.FullName);
        var destTableDict = destinationTables.ToDictionary(t => t.FullName);

        // Find tables only in source
        foreach (var table in sourceTables)
        {
            if (!destTableDict.ContainsKey(table.FullName))
            {
                result.TablesOnlyInSource.Add(table.FullName);
            }
        }

        // Find tables only in destination
        foreach (var table in destinationTables)
        {
            if (!sourceTableDict.ContainsKey(table.FullName))
            {
                result.TablesOnlyInDestination.Add(table.FullName);
            }
        }

        // Compare tables that exist in both
        foreach (var sourceTable in sourceTables)
        {
            if (destTableDict.TryGetValue(sourceTable.FullName, out var destTable))
            {
                var tableDiff = CompareTable(sourceTable, destTable);
                if (tableDiff.HasDifferences)
                {
                    result.TableDifferences.Add(tableDiff);
                }
            }
        }

        return result;
    }

    public DomainComparisonResult CompareMappedSchemas(List<DomainTableSchema> sourceTables, List<DomainTableSchema> destinationTables, List<TableMapping> mappings)
    {
        var result = new DomainComparisonResult();
        var sourceTableDict = sourceTables.ToDictionary(t => t.FullName);
        var destTableDict = destinationTables.ToDictionary(t => t.FullName);

        foreach (var mapping in mappings)
        {
            if (sourceTableDict.TryGetValue(mapping.SourceTable, out var sourceTable) &&
                destTableDict.TryGetValue(mapping.DestinationTable, out var destTable))
            {
                var tableDiff = CompareTable(sourceTable, destTable);
                tableDiff.TableName = $"{mapping.SourceTable} → {mapping.DestinationTable}";
                
                if (tableDiff.HasDifferences)
                {
                    result.TableDifferences.Add(tableDiff);
                }
            }
        }

        return result;
    }

    private DomainTableDifference CompareTable(DomainTableSchema sourceTable, DomainTableSchema destTable)
    {
        var diff = new DomainTableDifference
        {
            TableName = sourceTable.FullName
        };

        var sourceColDict = sourceTable.Columns.ToDictionary(c => c.ColumnName);
        var destColDict = destTable.Columns.ToDictionary(c => c.ColumnName);

        // Find columns only in source
        foreach (var col in sourceTable.Columns)
        {
            if (!destColDict.ContainsKey(col.ColumnName))
            {
                diff.ColumnsOnlyInSource.Add(col.ColumnName);
            }
        }

        // Find columns only in destination
        foreach (var col in destTable.Columns)
        {
            if (!sourceColDict.ContainsKey(col.ColumnName))
            {
                diff.ColumnsOnlyInDestination.Add(col.ColumnName);
            }
        }

        // Compare columns that exist in both
        foreach (var sourceCol in sourceTable.Columns)
        {
            if (destColDict.TryGetValue(sourceCol.ColumnName, out var destCol))
            {
                var colDiffs = new List<string>();

                if (sourceCol.DataType != destCol.DataType)
                    colDiffs.Add($"DataType: {sourceCol.DataType} vs {destCol.DataType}");

                if (sourceCol.MaxLength != destCol.MaxLength)
                    colDiffs.Add($"MaxLength: {sourceCol.MaxLength} vs {destCol.MaxLength}");

                if (sourceCol.NumericPrecision != destCol.NumericPrecision)
                    colDiffs.Add($"Precision: {sourceCol.NumericPrecision} vs {destCol.NumericPrecision}");

                if (sourceCol.NumericScale != destCol.NumericScale)
                    colDiffs.Add($"Scale: {sourceCol.NumericScale} vs {destCol.NumericScale}");

                if (sourceCol.IsNullable != destCol.IsNullable)
                    colDiffs.Add($"Nullable: {sourceCol.IsNullable} vs {destCol.IsNullable}");

                if (sourceCol.IsPrimaryKey != destCol.IsPrimaryKey)
                    colDiffs.Add($"PrimaryKey: {sourceCol.IsPrimaryKey} vs {destCol.IsPrimaryKey}");

                if (colDiffs.Any())
                {
                    diff.ColumnDifferences[sourceCol.ColumnName] = colDiffs;
                }
            }
        }

        return diff;
    }
}







