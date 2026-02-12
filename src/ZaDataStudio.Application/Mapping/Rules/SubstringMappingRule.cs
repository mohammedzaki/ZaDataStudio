using System.Text.RegularExpressions;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping.Rules;

/// <summary>
/// Rule 4: Handle substring/text extraction mappings
/// Priority: 4
/// Patterns: 
/// - SUBSTRING(column, start, length)
/// - LEFT(column, length)
/// - RIGHT(column, length)
/// - MID(column, start, length)
/// - SUBSTR(column, start, length)
/// </summary>
public class SubstringMappingRule : IMappingRule
{
    public bool CanHandle(DataColumnMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.MappingRule))
            return false;

        var rule = mapping.MappingRule.Trim().ToUpper();

        // Handle just "SUBSTRING" keyword
        if (rule == "SUBSTRING")
            return true;

        return rule.Contains("SUBSTRING") ||
               rule.Contains("LEFT(") ||
               rule.Contains("RIGHT(") ||
               rule.Contains("MID(") ||
               rule.Contains("SUBSTR(");
    }

    public MappingResult Apply(DataColumnMapping mapping, MappingContext context)
    {
        var expression = ParseSubstringExpression(mapping.MappingRule, mapping.OldTableName, mapping.NewDataType, mapping.OldColumn);
        var fullExpression = $"SELECT DISTINCT {expression} AS [{mapping.NewColumn}] FROM {FormatTableName(mapping.OldTableName)}";

        return new MappingResult
        {
            SqlExpression = expression,
            FullSqlExpression = fullExpression,
            HasWarning = expression.Contains("--"),
            Warning = expression.Contains("--") ? "Substring expression may need adjustment" : string.Empty,
            MappingRuleType = nameof(SubstringMappingRule)
        };
    }

    private string ParseSubstringExpression(string rule, string tableName, string destDataType, string sourceColumn)
    {
        var alias = GetTableAlias(tableName);
        rule = rule.Trim();

        // Pattern 0: Just "SUBSTRING" without parameters - use destination column max length
        if (rule.Equals("SUBSTRING", StringComparison.OrdinalIgnoreCase))
        {
            var maxLength = ExtractMaxLength(destDataType);
            if (maxLength.HasValue && !string.IsNullOrWhiteSpace(sourceColumn))
            {
                return $"SUBSTRING({alias}.[{sourceColumn}], 0, {maxLength.Value})";
            }
            else if (!string.IsNullOrWhiteSpace(sourceColumn))
            {
                return $"LEFT({alias}.[{sourceColumn}], 255) -- No max length specified, using 255";
            }
            else
            {
                return "-- Error: SUBSTRING requires source column";
            }
        }

        // Pattern 1: SUBSTRING(column, start, length)
        var substringPattern = @"SUBSTRING\s*\(\s*([a-zA-Z0-9_]+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)";
        var match = Regex.Match(rule, substringPattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var column = match.Groups[1].Value;
            var start = match.Groups[2].Value;
            var length = match.Groups[3].Value;
            return $"SUBSTRING({alias}.[{column}], {start}, {length})";
        }

        // Pattern 2: SUBSTR(column, start, length) - Oracle/MySQL style
        var substrPattern = @"SUBSTR\s*\(\s*([a-zA-Z0-9_]+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)";
        match = Regex.Match(rule, substrPattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var column = match.Groups[1].Value;
            var start = match.Groups[2].Value;
            var length = match.Groups[3].Value;
            return $"SUBSTRING({alias}.[{column}], {start}, {length})";
        }

        // Pattern 3: LEFT(column, length)
        var leftPattern = @"LEFT\s*\(\s*([a-zA-Z0-9_]+)\s*,\s*(\d+)\s*\)";
        match = Regex.Match(rule, leftPattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var column = match.Groups[1].Value;
            var length = match.Groups[2].Value;
            return $"LEFT({alias}.[{column}], {length})";
        }

        // Pattern 4: RIGHT(column, length)
        var rightPattern = @"RIGHT\s*\(\s*([a-zA-Z0-9_]+)\s*,\s*(\d+)\s*\)";
        match = Regex.Match(rule, rightPattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var column = match.Groups[1].Value;
            var length = match.Groups[2].Value;
            return $"RIGHT({alias}.[{column}], {length})";
        }

        // Pattern 5: MID(column, start, length) - Excel/VBA style
        var midPattern = @"MID\s*\(\s*([a-zA-Z0-9_]+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)";
        match = Regex.Match(rule, midPattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var column = match.Groups[1].Value;
            var start = match.Groups[2].Value;
            var length = match.Groups[3].Value;
            return $"SUBSTRING({alias}.[{column}], {start}, {length})";
        }

        // Pattern 6: Complex patterns like SUBSTRING(column, CHARINDEX(...), ...)
        if (rule.ToUpper().Contains("CHARINDEX") || rule.ToUpper().Contains("PATINDEX"))
        {
            // Replace column references with aliased versions
            return ReplaceColumnReferences(rule, alias);
        }

        // If already a valid SQL expression, return as-is with alias
        if (rule.ToUpper().StartsWith("SUBSTRING") || 
            rule.ToUpper().StartsWith("LEFT") || 
            rule.ToUpper().StartsWith("RIGHT"))
        {
            return ReplaceColumnReferences(rule, alias);
        }

        return $"-- Unsupported substring pattern: {rule}";
    }

    private string ReplaceColumnReferences(string expression, string alias)
    {
        // Replace bare column names with aliased versions
        // Pattern: word boundaries around column names
        var columnPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";
        
        var result = Regex.Replace(expression, columnPattern, match =>
        {
            var word = match.Value;
            
            // Don't replace SQL keywords
            var sqlKeywords = new[] { 
                "SUBSTRING", "LEFT", "RIGHT", "CHARINDEX", "PATINDEX", "LEN", 
                "UPPER", "LOWER", "TRIM", "LTRIM", "RTRIM", "CAST", "CONVERT",
                "AS", "FROM", "WHERE", "AND", "OR", "IS", "NULL", "NOT", "IN"
            };
            
            if (sqlKeywords.Contains(word.ToUpper()))
                return word;
            
            // Don't replace numbers
            if (int.TryParse(word, out _))
                return word;
            
            // Don't replace if already aliased
            if (match.Index > 0 && expression[match.Index - 1] == '.')
                return word;
            
            // Add alias
            return $"{alias}.[{word}]";
        }, RegexOptions.IgnoreCase);
        
        return result;
    }

    /// <summary>
    /// Extract maximum length from data type definition
    /// Examples: VARCHAR(50) -> 50, NVARCHAR(MAX) -> null, INT -> null
    /// </summary>
    private int? ExtractMaxLength(string dataType)
    {
        if (string.IsNullOrWhiteSpace(dataType))
            return null;

        // Pattern: datatype(length) or datatype(max)
        var lengthPattern = @"\((\d+)\)";
        var match = Regex.Match(dataType, lengthPattern);

        if (match.Success && int.TryParse(match.Groups[1].Value, out var length))
        {
            return length;
        }

        // Check for MAX
        if (dataType.ToUpper().Contains("(MAX)"))
        {
            return null; // MAX means no specific limit
        }

        // No length specified
        return null;
    }

    private string GetTableAlias(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return "src";

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

        var cleanName = tableName.Replace("[", "").Replace("]", "");
        var parts = cleanName.Split('.');
        return string.Join(".", parts.Select(p => $"[{p}]"));
    }
}
