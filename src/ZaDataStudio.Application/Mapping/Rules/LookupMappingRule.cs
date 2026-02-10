using System.Text.RegularExpressions;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping.Rules;

/// <summary>
/// Rule 3: Handle lookup/reference data mappings
/// Priority: 3
/// Format: [TableName].[ColumnName] = Value
/// Example: [LookupValues].[LookupTypeId] = 1600
/// </summary>
public class LookupMappingRule : IMappingRule
{
    public bool CanHandle(DataColumnMapping mapping)
    {
        return mapping.HasLookup;
    }

    public MappingResult Apply(DataColumnMapping mapping, MappingContext context)
    {
        // Try to parse new format first: [TableName].[ColumnName] = Value
        var newLookupSpec = ParseLookupSpecification(mapping.NewLookupTable);
        var oldLookupSpec = ParseLookupSpecification(mapping.OldLookupTable);

        if (newLookupSpec != null || oldLookupSpec != null)
        {
            var expression = GenerateLookupSQLWithFilter(mapping, newLookupSpec, oldLookupSpec, context);
            var result = new MappingResult
            {
                SqlExpression = expression
            };

            if (newLookupSpec != null)
            {
                result.Dependencies.Add(newLookupSpec.TableName);
            }
            if (oldLookupSpec != null && oldLookupSpec.TableName != newLookupSpec?.TableName)
            {
                result.Dependencies.Add(oldLookupSpec.TableName);
            }

            return result;
        }

        // Fallback to old pattern-based approach
        var lookupInfo = ParseLookupInfo(mapping);
        
        if (lookupInfo.IsValid)
        {
            var expression = GenerateLookupSQL(mapping, lookupInfo, context);
            return new MappingResult
            {
                SqlExpression = expression,
                Dependencies = { lookupInfo.LookupTable }
            };
        }

        // Fallback: comment for manual implementation
        return new MappingResult
        {
            SqlExpression = $"NULL",
            HasWarning = true,
            Warning = $"Lookup required but details not provided for {mapping.NewColumn}"
        };
    }

    /// <summary>
    /// Parse lookup specification format: [TableName].[ColumnName] = Value
    /// Example: [LookupValues].[LookupTypeId] = 1600
    /// </summary>
    private LookupSpecification? ParseLookupSpecification(string lookupSpec)
    {
        if (string.IsNullOrWhiteSpace(lookupSpec))
            return null;

        // Pattern: [TableName].[ColumnName] = Value
        var pattern = @"\[([^\]]+)\]\.\[([^\]]+)\]\s*=\s*(.+)";
        var match = Regex.Match(lookupSpec.Trim(), pattern);

        if (match.Success)
        {
            return new LookupSpecification
            {
                TableName = match.Groups[1].Value.Trim(),
                ColumnName = match.Groups[2].Value.Trim(),
                FilterValue = match.Groups[3].Value.Trim()
            };
        }

        return null;
    }

    private string GenerateLookupSQLWithFilter(
        DataColumnMapping mapping, 
        LookupSpecification? newLookupSpec, 
        LookupSpecification? oldLookupSpec,
        MappingContext context)
    {
        // Determine which lookup to use (prefer old for source, new for validation)
        var sourceLookupSpec = oldLookupSpec ?? newLookupSpec;
        
        if (sourceLookupSpec == null)
            return "NULL";

        var sourceTable = GetSourceTableAlias(mapping, context);
        var lookupTableName = FormatTableName(sourceLookupSpec.TableName);
        var lookupAlias = GetTableAlias(sourceLookupSpec.TableName);
        
        // Get the column to return from the lookup
        var returnColumn = !string.IsNullOrWhiteSpace(mapping.OldColumn) 
            ? mapping.OldColumn 
            : mapping.NewColumn;

        // Build subquery with filter
        var sql = $"(SELECT TOP 1 {lookupAlias}.[{returnColumn}] " +
                  $"FROM {lookupTableName} AS {lookupAlias} " +
                  $"WHERE {lookupAlias}.[{sourceLookupSpec.ColumnName}] = {sourceLookupSpec.FilterValue}";

        // If there's a source column, add join condition
        if (!string.IsNullOrWhiteSpace(mapping.OldColumn) && 
            !mapping.OldColumn.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            sql += $" AND {lookupAlias}.[SomeKeyColumn] = {sourceTable}.[{mapping.OldColumn}]";
        }

        sql += ")";

        return sql;
    }

    private LookupInfo ParseLookupInfo(DataColumnMapping mapping)
    {
        var info = new LookupInfo();
        var text = $"{mapping.MappingRule} {mapping.Notes} {mapping.NewColumnDescription}";
        
        // Pattern: "Lookup: TableName.ColumnName ON SourceColumn = LookupColumn"
        var pattern = @"lookup:\s*(\w+(?:\.\w+)?)\s*ON\s*(\w+)\s*=\s*(\w+)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            info.LookupTable = match.Groups[1].Value;
            info.SourceColumn = match.Groups[2].Value;
            info.LookupJoinColumn = match.Groups[3].Value;
            info.IsValid = true;
        }

        return info;
    }

    private string GenerateLookupSQL(DataColumnMapping mapping, LookupInfo lookup, MappingContext context)
    {
        var sourceTable = GetSourceTableAlias(mapping, context);
        var lookupAlias = GetTableAlias(lookup.LookupTable);
        
        return $"(SELECT TOP 1 {lookupAlias}.[{mapping.OldColumn}] " +
               $"FROM {FormatTableName(lookup.LookupTable)} AS {lookupAlias} " +
               $"WHERE {lookupAlias}.[{lookup.LookupJoinColumn}] = {sourceTable}.[{lookup.SourceColumn}])";
    }

    private string GetSourceTableAlias(DataColumnMapping mapping, MappingContext context)
    {
        return !string.IsNullOrWhiteSpace(mapping.OldTableName) 
            ? GetTableAlias(mapping.OldTableName) 
            : context.SourceTables.Any() 
                ? GetTableAlias(context.SourceTables.First()) 
                : "src";
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

    private string FormatTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return tableName;
        
        if (tableName.StartsWith("[") && tableName.Contains("].["))
            return tableName;
        
        // Add schema if not present
        if (!tableName.Contains("."))
            tableName = $"dbo.{tableName}";
        
        var cleanName = tableName.Replace("[", "").Replace("]", "");
        var parts = cleanName.Split('.');
        return string.Join(".", parts.Select(p => $"[{p}]"));
    }

    private class LookupInfo
    {
        public bool IsValid { get; set; }
        public string LookupTable { get; set; } = string.Empty;
        public string SourceColumn { get; set; } = string.Empty;
        public string LookupJoinColumn { get; set; } = string.Empty;
    }

    private class LookupSpecification
    {
        public string TableName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public string FilterValue { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"[{TableName}].[{ColumnName}] = {FilterValue}";
        }
    }
}
