using Microsoft.Data.SqlClient;
using System.Data;

namespace ZaDataStudio.Web.Services;

public class SqlServerComparisonService
{
    public async Task<ConnectionTestResult> TestConnectionAsync(string connectionString)
    {
        var result = new ConnectionTestResult();
        var startTime = DateTime.UtcNow;

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var command = new SqlCommand("SELECT @@VERSION as Version, DB_NAME() as DatabaseName, @@SERVERNAME as ServerName", connection);
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                result.IsSuccessful = true;
                result.ServerName = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2);
                result.DatabaseName = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
                result.Version = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
            }

            result.ResponseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;
            result.ResponseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        }

        return result;
    }

    public async Task<List<string>> GetTableNamesAsync(string connectionString)
    {
        var tableNames = new List<string>();

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT TABLE_SCHEMA + '.' + TABLE_NAME as FullName
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_SCHEMA, TABLE_NAME";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tableNames.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving table names: {ex.Message}", ex);
        }

        return tableNames;
    }

    public async Task<List<ColumnTypeInfo>> GetColumnTypesAsync(string connectionString, string tableName)
    {
        var columns = new List<ColumnTypeInfo>();

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    COLUMN_NAME,
                    DATA_TYPE,
                    CHARACTER_MAXIMUM_LENGTH,
                    NUMERIC_PRECISION,
                    NUMERIC_SCALE,
                    IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA + '.' + TABLE_NAME = @tableName
                ORDER BY ORDINAL_POSITION";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@tableName", tableName);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnTypeInfo
                {
                    ColumnName = reader.GetString(0),
                    DataType = reader.GetString(1),
                    MaxLength = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    NumericPrecision = reader.IsDBNull(3) ? null : reader.GetByte(3),
                    NumericScale = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    IsNullable = reader.GetString(5) == "YES"
                });
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving column types: {ex.Message}", ex);
        }

        return columns;
    }

    public ColumnTypeComparisonResult CompareColumnTypes(
        List<ColumnTypeInfo> sourceColumns,
        List<ColumnTypeInfo> destColumns,
        List<ColumnMapping> mappings)
    {
        var result = new ColumnTypeComparisonResult();

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
                result.Differences.Add(new ColumnTypeDifference
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

    public async Task<List<TableSchema>> GetTableSchemasAsync(string connectionString)
    {
        var tables = new List<TableSchema>();

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

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

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            TableSchema? currentTable = null;

            while (await reader.ReadAsync())
            {
                var schema = reader.GetString(0);
                var tableName = reader.GetString(1);
                var fullTableName = $"{schema}.{tableName}";

                if (currentTable == null || currentTable.FullName != fullTableName)
                {
                    currentTable = new TableSchema
                    {
                        Schema = schema,
                        TableName = tableName,
                        FullName = fullTableName,
                        Columns = new List<ColumnSchema>()
                    };
                    tables.Add(currentTable);
                }

                if (!reader.IsDBNull(2))
                {
                    var column = new ColumnSchema
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

    public async Task<List<TableSchema>> GetTableSchemasAsync(string connectionString, List<string> tableNames)
    {
        var tables = new List<TableSchema>();

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

            TableSchema? currentTable = null;

            while (await reader.ReadAsync())
            {
                var schema = reader.GetString(0);
                var tableName = reader.GetString(1);
                var fullTableName = $"{schema}.{tableName}";

                if (currentTable == null || currentTable.FullName != fullTableName)
                {
                    currentTable = new TableSchema
                    {
                        Schema = schema,
                        TableName = tableName,
                        FullName = fullTableName,
                        Columns = new List<ColumnSchema>()
                    };
                    tables.Add(currentTable);
                }

                if (!reader.IsDBNull(2))
                {
                    var column = new ColumnSchema
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

    public ComparisonResult CompareSchemas(List<TableSchema> sourceTables, List<TableSchema> destinationTables)
    {
        var result = new ComparisonResult();

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

    public ComparisonResult CompareMappedSchemas(List<TableSchema> sourceTables, List<TableSchema> destinationTables, List<TableMapping> mappings)
    {
        var result = new ComparisonResult();
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

    private TableDifference CompareTable(TableSchema sourceTable, TableSchema destTable)
    {
        var diff = new TableDifference
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

public class TableSchema
{
    public string Schema { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<ColumnSchema> Columns { get; set; } = new();
}

public class ColumnSchema
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public byte? NumericPrecision { get; set; }
    public int? NumericScale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }

    public string DisplayType
    {
        get
        {
            var type = DataType;
            if (MaxLength.HasValue && MaxLength.Value > 0)
                type += $"({MaxLength})";
            else if (NumericPrecision.HasValue)
            {
                type += $"({NumericPrecision}";
                if (NumericScale.HasValue)
                    type += $",{NumericScale}";
                type += ")";
            }
            return type;
        }
    }
}

public class ComparisonResult
{
    public List<string> TablesOnlyInSource { get; set; } = new();
    public List<string> TablesOnlyInDestination { get; set; } = new();
    public List<TableDifference> TableDifferences { get; set; } = new();

    public bool HasDifferences =>
        TablesOnlyInSource.Any() ||
        TablesOnlyInDestination.Any() ||
        TableDifferences.Any(d => d.HasDifferences);
}

public class TableDifference
{
    public string TableName { get; set; } = string.Empty;
    public List<string> ColumnsOnlyInSource { get; set; } = new();
    public List<string> ColumnsOnlyInDestination { get; set; } = new();
    public Dictionary<string, List<string>> ColumnDifferences { get; set; } = new();

    public bool HasDifferences =>
        ColumnsOnlyInSource.Any() ||
        ColumnsOnlyInDestination.Any() ||
        ColumnDifferences.Any();
}

public class ConnectionTestResult
{
    public bool IsSuccessful { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public double ResponseTime { get; set; }
}

public class TableMapping
{
    public string SourceTable { get; set; } = string.Empty;
    public List<string> SourceTables { get; set; } = new(); // Multiple source tables
    public string DestinationTable { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = true;
    public List<ColumnMapping> ColumnMappings { get; set; } = new();
    public bool CompareData { get; set; } = false;
    public List<string> AvailableSourceColumns { get; set; } = new();
    public List<string> AvailableDestinationColumns { get; set; } = new();
    
    // Helper to get all source tables (single or multiple)
    public List<string> GetAllSourceTables() 
    {
        if (SourceTables.Any())
            return SourceTables;
        if (!string.IsNullOrEmpty(SourceTable))
            return new List<string> { SourceTable };
        return new List<string>();
    }
}

public class ColumnMapping
{
    public string SourceTable { get; set; } = string.Empty; // Which source table this column comes from
    public string SourceColumn { get; set; } = string.Empty;
    public string DestinationColumn { get; set; } = string.Empty;
    public bool IsKey { get; set; } = false;
}

public class ColumnTypeInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public byte? NumericPrecision { get; set; }
    public int? NumericScale { get; set; }
    public bool IsNullable { get; set; }

    public string DisplayType
    {
        get
        {
            var type = DataType;
            if (MaxLength.HasValue && MaxLength.Value > 0)
                type += $"({MaxLength})";
            else if (NumericPrecision.HasValue)
            {
                type += $"({NumericPrecision}";
                if (NumericScale.HasValue)
                    type += $",{NumericScale}";
                type += ")";
            }
            return type + (IsNullable ? " NULL" : " NOT NULL");
        }
    }
}

public class ColumnTypeComparisonResult
{
    public List<ColumnTypeDifference> Differences { get; set; } = new();
    
    public bool HasDifferences => Differences.Any();
}

public class ColumnTypeDifference
{
    public string SourceColumn { get; set; } = string.Empty;
    public string DestinationColumn { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string DestinationType { get; set; } = string.Empty;
    public List<string> DifferenceDetails { get; set; } = new();
}





