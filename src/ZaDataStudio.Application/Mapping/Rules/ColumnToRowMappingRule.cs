using System.Text.RegularExpressions;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping.Rules;

/// <summary>
/// Rule 8: Handle column-to-row transformations (unpivot)
/// Maps multiple source columns to destination rows with specific IDs
/// Example: Facebook, Twitter columns -> SocialMediaPlatformId with values 201, 202, etc.
/// Priority: 2 (High - needs to run before direct mapping)
/// </summary>
public class ColumnToRowMappingRule : IMappingRule
{
    public int Priority => 2;

    public bool CanHandle(DataColumnMapping mapping)
    {
        var text = $"{mapping.MappingRule} {mapping.Notes}";
        
        // Look for patterns indicating column-to-row mapping:
        // "Facebook=201, Youtube=202" or "201 Facebook" or similar
        return ContainsColumnToRowPattern(text);
    }

    public MappingResult Apply(DataColumnMapping mapping, MappingContext context)
    {
        var columnMappings = ParseColumnMappings(mapping);

        if (columnMappings.Count == 0)
        {
            return new MappingResult
            {
                SqlExpression = "NULL",
                HasWarning = true,
                Warning = "No valid column-to-row mappings found in MappingRule or Notes",
                MappingRuleType = nameof(ColumnToRowMappingRule)
            };
        }

        var sqlStatements = GenerateUnpivotSql(mapping, columnMappings, context);

        return new MappingResult
        {
            FullSqlExpression = sqlStatements,
            SqlExpression = $"-- Column-to-row mapping for {mapping.NewColumn}",
            Dependencies = columnMappings.Select(cm => cm.SourceColumn).ToList(),
            MappingRuleType = nameof(ColumnToRowMappingRule)
        };
    }

    private bool ContainsColumnToRowPattern(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.ToLower();
        
        // Look for patterns like:
        // "201 Facebook" or "Facebook=201" or "Facebook:201" or "Facebook->201"
        var pattern = @"(\w+)\s*[=:->]+\s*\d+|\d+\s+\w+";
        
        return text.Contains("unpivot") || 
               text.Contains("column to row") ||
               text.Contains("columns to rows");
    }

    private List<ColumnMapping> ParseColumnMappings(DataColumnMapping mapping)
    {
        var text = $"{mapping.MappingRule} {mapping.Notes}";
        var mappings = new List<ColumnMapping>();

        // Pattern 1: "Facebook=201" or "Facebook:201" or "Facebook->201"
        var pattern1 = @"(\w+)\s*[=:->]+\s*(\d+)";
        var matches1 = Regex.Matches(text, pattern1);
        
        foreach (Match match in matches1)
        {
            mappings.Add(new ColumnMapping
            {
                SourceColumn = match.Groups[1].Value,
                DestinationValue = match.Groups[2].Value
            });
        }

        // Pattern 2: "201 Facebook" or "201	Facebook" (with tab)
        var pattern2 = @"(\d+)\s+(\w+)";
        var matches2 = Regex.Matches(text, pattern2);
        
        foreach (Match match in matches2)
        {
            // Avoid duplicates
            var columnName = match.Groups[2].Value;
            if (!mappings.Any(m => m.SourceColumn.Equals(columnName, StringComparison.OrdinalIgnoreCase)))
            {
                mappings.Add(new ColumnMapping
                {
                    SourceColumn = columnName,
                    DestinationValue = match.Groups[1].Value
                });
            }
        }

        return mappings;
    }

    private string GenerateUnpivotSql(
        DataColumnMapping mapping, 
        List<ColumnMapping> columnMappings, 
        MappingContext context)
    {
        var sourceTable = !string.IsNullOrWhiteSpace(mapping.OldTableName) 
            ? mapping.OldTableName 
            : context.SourceTables.FirstOrDefault() ?? "SourceTable";

        var sourceAlias = GetTableAlias(sourceTable);
        var destinationTable = mapping.NewTableName;
        var destinationColumn = mapping.NewColumn;
        var allowNull = mapping.NewColumnNullable.GetValueOrDefault(true);

        var sqlBuilder = new System.Text.StringBuilder();
        sqlBuilder.AppendLine($"-- Unpivot mapping for {destinationColumn} using CROSS APPLY");
        sqlBuilder.AppendLine($"INSERT INTO {destinationTable} ({destinationColumn}, ... other columns ...)");
        sqlBuilder.AppendLine("SELECT");
        sqlBuilder.AppendLine($"    {sourceAlias}.[KeyColumn],    -- replace with your row key column");
        sqlBuilder.AppendLine($"    m.{destinationColumn},");
        sqlBuilder.AppendLine("    m.Value                        -- replace with actual destination column name");
        sqlBuilder.AppendLine($"FROM {sourceTable} AS {sourceAlias}");
        sqlBuilder.AppendLine("CROSS APPLY (");
        sqlBuilder.AppendLine("    VALUES");

        // Generate VALUES rows for each column mapping
        for (int i = 0; i < columnMappings.Count; i++)
        {
            var columnMap = columnMappings[i];
            var comma = i < columnMappings.Count - 1 ? "," : "";
            sqlBuilder.AppendLine($"        ({columnMap.DestinationValue}, {sourceAlias}.[{columnMap.SourceColumn}]){comma}");
        }

        sqlBuilder.AppendLine($") AS m({destinationColumn}, Value)");

        // Add WHERE clause for NULL handling
        if (!allowNull)
        {
            sqlBuilder.AppendLine("WHERE");
            sqlBuilder.AppendLine("    m.Value IS NOT NULL");
            sqlBuilder.AppendLine("    AND NULLIF(LTRIM(RTRIM(m.Value)), '') IS NOT NULL;");
        }
        else
        {
            sqlBuilder.AppendLine(";");
        }

        return sqlBuilder.ToString().TrimEnd();
    }

    private string GetTableAlias(string tableName)
    {
        var cleanName = tableName.Replace("[", "").Replace("]", "");
        var parts = cleanName.Split('.');
        var name = parts.Length > 1 ? parts[1] : parts[0];
        
        var alias = string.Concat(name.Where(char.IsUpper).Select(char.ToLower));
        if (string.IsNullOrEmpty(alias))
        {
            alias = name.Substring(0, Math.Min(2, name.Length)).ToLower();
        }
        
        return alias;
    }

    private class ColumnMapping
    {
        public string SourceColumn { get; set; } = string.Empty;
        public string DestinationValue { get; set; } = string.Empty;
    }
}
