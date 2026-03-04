using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Microsoft.Data.SqlClient;
using ZaDataStudio.Application.Common.Interfaces;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping;

public class MappingComparisonService : IMappingComparisonService
{
    private string _sourceConnectionString;
    private string _destinationConnectionString;
    private readonly IDatabaseService _databaseService;
    private readonly ILookupColumnAnalyzer _lookupAnalyzer;

    public MappingComparisonService(
        IDatabaseService databaseService,
        ILookupColumnAnalyzer lookupAnalyzer)
    {
        _databaseService = databaseService;
        _lookupAnalyzer = lookupAnalyzer;
    }

    public async Task<MappingComparisonResult> CompareMappingsAsync(
        DataMappingConfiguration sourceMapping,
        string sourceConnectionString,
        string destinationConnectionString,
        IProgress<AnalysisProgress>? progress = null)
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

                // 1. Check lookup columns (with new format support)
                if (columnMapping.HasLookup)
                {
                    if (!string.IsNullOrWhiteSpace(columnMapping.NewLookupTable) ||
                        !string.IsNullOrWhiteSpace(columnMapping.OldLookupTable))
                    {
                        try
                        {
                            var lookupAnalysis = await _lookupAnalyzer.AnalyzeLookupColumnWithSpecAsync(
                                columnMapping,
                                _sourceConnectionString,
                                _destinationConnectionString,
                                progress);

                            lookupAnalysis.SourceTable = columnMapping.OldTableName;
                            lookupAnalysis.SourceColumn = columnMapping.OldColumn;
                            lookupAnalysis.TableName = destTableName;
                            lookupAnalysis.ColumnName = columnMapping.NewColumn;

                            comparisonResult.LookupAnalysis.Add(lookupAnalysis);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error analyzing lookup {destTableName}.{columnMapping.NewColumn}: {ex.Message}");
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(columnMapping.MappingRule) && columnMapping.MappingStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                    {
                        // If there's a mapping rule, try to analyze as lookup
                        try
                        {
                            var lookupAnalysis = await _lookupAnalyzer.AnalyzeLookupColumnAsync(
                                columnMapping,
                                _sourceConnectionString,
                                _destinationConnectionString,
                                progress);

                            lookupAnalysis.SourceTable = columnMapping.OldTableName;
                            lookupAnalysis.SourceColumn = columnMapping.OldColumn;
                            lookupAnalysis.TableName = destTableName;
                            lookupAnalysis.ColumnName = columnMapping.NewColumn;
                            comparisonResult.LookupAnalysis.Add(lookupAnalysis);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error analyzing mapping rule lookup {destTableName}.{columnMapping.NewColumn}: {ex.Message}");
                        }
                    }
                }

                // 2. Compare datatypes (skip if has mapping rule or is lookup)
                if (string.IsNullOrWhiteSpace(columnMapping.MappingRule) &&
                    !columnMapping.HasLookup &&
                    string.IsNullOrWhiteSpace(columnMapping.NewLookupTable) &&
                    string.IsNullOrWhiteSpace(columnMapping.OldLookupTable) ||
                    columnMapping.MappingStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                {
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
        }
        return comparisonResult;
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

        var query = @"
            SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @tableSchema AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName";
        var maxLenQuery = @$"SELECT MAX(LEN({sourceColumn})) AS MaxLength FROM {(sourceTable.Contains(".") ? sourceTable.Split('.')[0].Replace("[", "").Replace("]", "") : "dbo")}.{sourceTable};";

        // Get actual datatypes from source database using database service - connection reused
        var srcMaxLenResult = await _databaseService.ExecuteScalarAsync(_sourceConnectionString, maxLenQuery);
        var srcMaxLength = srcMaxLenResult == null ? (int?)null : Convert.ToInt32(srcMaxLenResult);

        using var srcReader = await _databaseService.ExecuteReaderAsync(_sourceConnectionString, query, cmd =>
        {
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue("@tableSchema", sourceTable.Contains(".") ? sourceTable.Split('.')[0].Replace("[", "").Replace("]", "") : "dbo");
            cmd.Parameters.AddWithValue("@tableName", sourceTable);
            cmd.Parameters.AddWithValue("@columnName", sourceColumn);
        });

        if (await srcReader.ReadAsync())
        {
            var dataType = srcReader.GetString(0);
            //var maxLength = srcReader.IsDBNull(1) ? (int?)null : srcReader.GetInt32(1);
            var precision = srcReader.IsDBNull(2) ? (byte?)null : srcReader.GetByte(2);
            var scale = srcReader.IsDBNull(3) ? (int?)null : srcReader.GetInt32(3);

            comparison.SourceDataType = FormatDataType(dataType, srcMaxLength, precision, scale);
        }
        await srcReader.DisposeAsync();

        // Get actual datatypes from destination database using database service - connection reused
        using var destReader = await _databaseService.ExecuteReaderAsync(_destinationConnectionString, query, cmd =>
        {
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue("@tableSchema", destTable.Contains(".") ? destTable.Split('.')[0].Replace("[", "").Replace("]", "") : "dbo");
            cmd.Parameters.AddWithValue("@tableName", destTable);
            cmd.Parameters.AddWithValue("@columnName", destColumn);
        });
        if (await destReader.ReadAsync())
        {
            var dataType = destReader.GetString(0);
            var maxLength = destReader.IsDBNull(1) ? (int?)null : destReader.GetInt32(1);
            var precision = destReader.IsDBNull(2) ? (byte?)null : destReader.GetByte(2);
            var scale = destReader.IsDBNull(3) ? (int?)null : destReader.GetInt32(3);

            comparison.DestinationDataType = FormatDataType(dataType, maxLength, precision, scale);
        }
        await destReader.DisposeAsync();

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

