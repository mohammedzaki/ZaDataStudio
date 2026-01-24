using System.Text.RegularExpressions;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping.Rules;

/// <summary>
/// Rule 3: Handle lookup/reference data mappings
/// Priority: 3
/// </summary>
public class LookupMappingRule : IMappingRule
{
    public int Priority => 3;

    public bool CanHandle(DataColumnMapping mapping)
    {
        return mapping.HasLookup || 
               mapping.MappingRule?.Contains("lookup", StringComparison.OrdinalIgnoreCase) == true;
    }

    public MappingResult Apply(DataColumnMapping mapping, MappingContext context)
    {
        // Extract lookup table info from Notes or MappingRule
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
            SqlExpression = $"/* TODO: Implement lookup for {mapping.OldColumn} */ NULL",
            HasWarning = true,
            Warning = $"Lookup required but details not provided for {mapping.NewColumn}"
        };
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
               $"FROM {lookup.LookupTable} {lookupAlias} " +
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

    private class LookupInfo
    {
        public bool IsValid { get; set; }
        public string LookupTable { get; set; } = string.Empty;
        public string SourceColumn { get; set; } = string.Empty;
        public string LookupJoinColumn { get; set; } = string.Empty;
    }
}
