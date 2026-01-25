using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.SqlClient;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping;

public class MappingComparisonService : IMappingComparisonService
{
    private string _sourceConnectionString;
    private string _destinationConnectionString;

    public async Task<MappingComparisonResult> CompareMappingsAsync(
        DataMappingConfiguration sourceMapping,
        string sourceConnectionString,
        string destinationConnectionString)
    {
        _sourceConnectionString = sourceConnectionString;
        _destinationConnectionString = destinationConnectionString;
        var comparisonResult = new MappingComparisonResult();
        foreach (var tableGroup in sourceMapping.GroupedByTable)
        {
            var destTableName = tableGroup.Key;

            foreach (var columnMapping in tableGroup.Value)
            {
                // Skip if no source specified (N/A, null, or empty)
                if (string.IsNullOrWhiteSpace(columnMapping.OldTableName) ||
                    columnMapping.OldTableName.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(columnMapping.OldColumn) ||
                    columnMapping.OldColumn.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip expressions (they contain SQL operators)
                if (columnMapping.OldColumn.Contains("+") ||
                    columnMapping.OldColumn.Contains("CASE") ||
                    columnMapping.OldColumn.Contains("("))
                    continue;

                // 1. Check lookup columns
                if (columnMapping.HasLookup)
                {
                    try
                    {
                        var lookupAnalysis = await AnalyzeLookupColumn(
                            columnMapping.OldTableName,
                            columnMapping.OldColumn,
                            destTableName,
                            columnMapping.NewColumn);

                        lookupAnalysis.SourceTable = columnMapping.OldTableName;
                        lookupAnalysis.SourceColumn = columnMapping.OldColumn;
                        lookupAnalysis.TableName = destTableName;
                        lookupAnalysis.ColumnName = columnMapping.NewColumn;

                        comparisonResult.LookupAnalysis.Add(lookupAnalysis);
                    }
                    catch (Exception ex)
                    {
                        // Log but continue with other columns
                        Console.WriteLine($"Error analyzing lookup {destTableName}.{columnMapping.NewColumn}: {ex.Message}");
                    }
                }

                // 2. Compare datatypes
                try
                {
                    var datatypeComparison = await CompareDatatypes(
                        columnMapping.OldTableName,
                        columnMapping.OldColumn,
                        destTableName,
                        columnMapping.NewColumn,
                        columnMapping.OldDataType,
                        columnMapping.NewDataType);

                    comparisonResult.DatatypeComparisons.Add(datatypeComparison);
                }
                catch (Exception ex)
                {
                    // Log but continue with other columns
                    Console.WriteLine($"Error comparing datatype {destTableName}.{columnMapping.NewColumn}: {ex.Message}");
                }
            }
        }
        return comparisonResult;
    }

    private async Task<LookupColumnAnalysis> AnalyzeLookupColumn(
        string sourceTable, string sourceColumn,
        string destTable, string destColumn)
    {
        var analysis = new LookupColumnAnalysis();

        // Load source data
        using (var sourceConn = new SqlConnection(_sourceConnectionString))
        {
            await sourceConn.OpenAsync();

            // Get distinct values from source
            var query = $@"
                SELECT DISTINCT TOP 100 [{sourceColumn}] 
                FROM {sourceTable} 
                WHERE [{sourceColumn}] IS NOT NULL 
                ORDER BY [{sourceColumn}]";

            using (var cmd = new SqlCommand(query, sourceConn))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    analysis.SourceSampleValues.Add(reader[0]?.ToString() ?? "");
                }
            } // Close reader before next query

            // Get distinct count
            var countQuery = $"SELECT COUNT(DISTINCT [{sourceColumn}]) FROM {sourceTable}";
            using var countCmd = new SqlCommand(countQuery, sourceConn);
            analysis.SourceDistinctCount = (int)(await countCmd.ExecuteScalarAsync() ?? 0);
        }

        // Load destination data
        using (var destConn = new SqlConnection(_destinationConnectionString))
        {
            await destConn.OpenAsync();

            // Get distinct values from destination
            var query = $@"
                SELECT DISTINCT TOP 100 [{destColumn}] 
                FROM {destTable} 
                WHERE [{destColumn}] IS NOT NULL 
                ORDER BY [{destColumn}]";

            using (var cmd = new SqlCommand(query, destConn))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    analysis.DestinationSampleValues.Add(reader[0]?.ToString() ?? "");
                }
            } // Close reader before next query

            // Get distinct count
            var countQuery = $"SELECT COUNT(DISTINCT [{destColumn}]) FROM {destTable}";
            using var countCmd = new SqlCommand(countQuery, destConn);
            analysis.DestinationDistinctCount = (int)(await countCmd.ExecuteScalarAsync() ?? 0);
        }

        // Find mismatched values (in source but not in destination)
        var destValuesSet = analysis.DestinationSampleValues.ToHashSet(StringComparer.OrdinalIgnoreCase);
        analysis.MismatchedValues = analysis.SourceSampleValues
            .Where(v => !destValuesSet.Contains(v))
            .ToList();

        return analysis;
    }

    private async Task<DatatypeComparison> CompareDatatypes(
        string sourceTable, string sourceColumn,
        string destTable, string destColumn,
        string excelSourceType, string excelDestType)
    {
        var comparison = new DatatypeComparison
        {
            SourceTable = sourceTable,
            SourceColumn = sourceColumn,
            DestinationTable = destTable,
            DestinationColumn = destColumn
        };

        // Get actual datatypes from source database
        using (var sourceConn = new SqlConnection(_sourceConnectionString))
        {
            await sourceConn.OpenAsync();

            var query = @"
                SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @tableSchema AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName";

            using (var cmd = new SqlCommand(query, sourceConn))
            {
                cmd.Parameters.AddWithValue("@tableSchema", sourceTable.Contains(".") ? sourceTable.Split('.')[0].Replace("[", "").Replace("]", "") : "dbo");
                cmd.Parameters.AddWithValue("@tableName", sourceTable);
                cmd.Parameters.AddWithValue("@columnName", sourceColumn);

                using var reader = await cmd.ExecuteReaderAsync();
                var str = cmd.ToString();

                if (await reader.ReadAsync())
                {
                    var dataType = reader.GetString(0);
                    var maxLength = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                    var precision = reader.IsDBNull(2) ? (byte?)null : reader.GetByte(2);
                    var scale = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);

                    comparison.SourceDataType = FormatDataType(dataType, maxLength, precision, scale);
                }
            }
        }

        // Get actual datatypes from destination database
        using (var destConn = new SqlConnection(_destinationConnectionString))
        {
            await destConn.OpenAsync();

            var query = @"
                SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @tableSchema AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName";

            using (var cmd = new SqlCommand(query, destConn))
            {
                cmd.Parameters.AddWithValue("@tableSchema", destTable.Contains(".") ? destTable.Split('.')[0].Replace("[", "").Replace("]", "") : "dbo");
                cmd.Parameters.AddWithValue("@tableName", destTable);
                cmd.Parameters.AddWithValue("@columnName", destColumn);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var dataType = reader.GetString(0);
                    var maxLength = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                    var precision = reader.IsDBNull(2) ? (byte?)null : reader.GetByte(2);
                    var scale = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);

                    comparison.DestinationDataType = FormatDataType(dataType, maxLength, precision, scale);
                }
            }
        }

        // Compare and identify issues
        CompareTypesAndFindIssues(comparison, excelSourceType, excelDestType);

        return comparison;
    }

    private string FormatDataType(string dataType, int? maxLength, byte? precision, int? scale)
    {
        dataType = dataType.ToUpper();

        if (maxLength.HasValue && maxLength.Value > 0 &&
            (dataType == "VARCHAR" || dataType == "NVARCHAR" || dataType == "CHAR" || dataType == "NCHAR"))
        {
            return maxLength.Value == -1 ? $"{dataType}(MAX)" : $"{dataType}({maxLength.Value})";
        }

        if (precision.HasValue && (dataType == "DECIMAL" || dataType == "NUMERIC"))
        {
            return scale.HasValue ? $"{dataType}({precision},{scale})" : $"{dataType}({precision})";
        }

        return dataType;
    }

    private void CompareTypesAndFindIssues(DatatypeComparison comparison, string excelSourceType, string excelDestType)
    {
        var sourceBase = comparison.SourceDataType.Split('(')[0].ToUpper();
        var destBase = comparison.DestinationDataType.Split('(')[0].ToUpper();

        // Type mismatch
        if (sourceBase != destBase)
        {
            // Check if it's a compatible conversion
            if (!IsCompatibleConversion(sourceBase, destBase))
            {
                comparison.Issues.Add($"Type mismatch: {sourceBase} → {destBase}");
            }
            else
            {
                comparison.Issues.Add($"Type conversion needed: {sourceBase} → {destBase}");
            }
        }

        // Length comparison for string types
        if ((sourceBase == "VARCHAR" || sourceBase == "NVARCHAR" || sourceBase == "CHAR" || sourceBase == "NCHAR") &&
            (destBase == "VARCHAR" || destBase == "NVARCHAR" || destBase == "CHAR" || destBase == "NCHAR"))
        {
            var sourceLength = ExtractLength(comparison.SourceDataType);
            var destLength = ExtractLength(comparison.DestinationDataType);

            if (sourceLength.HasValue && destLength.HasValue)
            {
                if (sourceLength.Value > destLength.Value)
                {
                    comparison.Issues.Add($"Potential data truncation: source length ({sourceLength}) > destination length ({destLength})");
                }
            }
        }

        // Precision comparison for numeric types
        if ((sourceBase == "DECIMAL" || sourceBase == "NUMERIC") &&
            (destBase == "DECIMAL" || destBase == "NUMERIC"))
        {
            var sourcePrecision = ExtractPrecision(comparison.SourceDataType);
            var destPrecision = ExtractPrecision(comparison.DestinationDataType);

            if (sourcePrecision.HasValue && destPrecision.HasValue)
            {
                if (sourcePrecision.Value > destPrecision.Value)
                {
                    comparison.Issues.Add($"Potential precision loss: source ({sourcePrecision}) > destination ({destPrecision})");
                }
            }

            var sourceScale = ExtractScale(comparison.SourceDataType);
            var destScale = ExtractScale(comparison.DestinationDataType);

            if (sourceScale.HasValue && destScale.HasValue)
            {
                if (sourceScale.Value > destScale.Value)
                {
                    comparison.Issues.Add($"Potential scale loss: source scale ({sourceScale}) > destination scale ({destScale})");
                }
            }
        }

        // Check Excel types match actual
        if (!string.IsNullOrWhiteSpace(excelSourceType))
        {
            var excelSourceBase = excelSourceType.Split('(')[0].ToUpper().Trim();
            if (excelSourceBase != sourceBase && !IsCompatibleConversion(excelSourceBase, sourceBase))
            {
                comparison.Issues.Add($"Excel source type ({excelSourceType}) differs from actual ({comparison.SourceDataType})");
            }
        }

        if (!string.IsNullOrWhiteSpace(excelDestType))
        {
            var excelDestBase = excelDestType.Split('(')[0].ToUpper().Trim();
            if (excelDestBase != destBase && !IsCompatibleConversion(excelDestBase, destBase))
            {
                comparison.Issues.Add($"Excel dest type ({excelDestType}) differs from actual ({comparison.DestinationDataType})");
            }
        }
    }

    private bool IsCompatibleConversion(string sourceType, string destType)
    {
        var compatiblePairs = new[]
        {
            ("INT", "BIGINT"),
            ("SMALLINT", "INT"),
            ("TINYINT", "SMALLINT"),
            ("VARCHAR", "NVARCHAR"),
            ("CHAR", "NCHAR"),
            ("DATE", "DATETIME"),
            ("DATETIME2", "DATETIME"),
            ("FLOAT", "DECIMAL"),
            ("REAL", "FLOAT")
        };

        return compatiblePairs.Any(pair =>
            pair.Item1 == sourceType && pair.Item2 == destType);
    }

    private int? ExtractLength(string dataType)
    {
        var match = System.Text.RegularExpressions.Regex.Match(dataType, @"\((\d+)\)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var length) ? length : null;
    }

    private int? ExtractPrecision(string dataType)
    {
        var match = System.Text.RegularExpressions.Regex.Match(dataType, @"\((\d+),");
        return match.Success && int.TryParse(match.Groups[1].Value, out var precision) ? precision : null;
    }

    private int? ExtractScale(string dataType)
    {
        var match = System.Text.RegularExpressions.Regex.Match(dataType, @",(\d+)\)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var scale) ? scale : null;
    }
}
