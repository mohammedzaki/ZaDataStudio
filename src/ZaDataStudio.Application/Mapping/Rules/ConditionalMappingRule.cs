using System.Text.RegularExpressions;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping.Rules;

/// <summary>
/// Rule 5: Handle conditional mappings (CASE WHEN)
/// Priority: 5
/// Patterns: 
/// - "IF condition THEN value1 ELSE value2"
/// - CASE WHEN...THEN...WHEN...THEN...ELSE...END (multiple conditions)
/// </summary>
public class ConditionalMappingRule : IMappingRule
{
    public bool CanHandle(DataColumnMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.MappingRule))
            return false;

        return mapping.MappingRule.Contains("IF", StringComparison.OrdinalIgnoreCase) ||
               mapping.MappingRule.Contains("WHEN", StringComparison.OrdinalIgnoreCase) ||
               mapping.MappingRule.Contains("CASE", StringComparison.OrdinalIgnoreCase) ||
               mapping.MappingRule.Contains("?");
    }

    public MappingResult Apply(DataColumnMapping mapping, MappingContext context)
    {
        var expression = ConvertToCase(mapping.MappingRule, mapping.OldTableName);
        var fullExpression = $"SELECT DISTINCT {expression} AS [{mapping.NewColumn}] FROM {FormatTableName(mapping.OldTableName)}";

        return new MappingResult
        {
            SqlExpression = expression,
            FullSqlExpression = fullExpression,
            HasWarning = !expression.Contains("END"),
            Warning = !expression.Contains("END") ? "Conditional expression may be incomplete" : string.Empty,
            MappingRuleType = nameof(ConditionalMappingRule)
        };
    }

    private string ConvertToCase(string rule, string tableName)
    {
        var alias = GetTableAlias(tableName);
        rule = rule.Trim();

        // Check if already a CASE statement
        if (rule.Contains("CASE", StringComparison.OrdinalIgnoreCase))
        {
            // Replace column references with aliased versions
            return ReplaceColumnReferences(rule, alias, " ");
        }

        // Simple IF...THEN...ELSE conversion
        var pattern = @"IF\s+(.+?)\s+THEN\s+(.+?)\s+ELSE\s+(.+)";
        var match = Regex.Match(rule, pattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var condition = match.Groups[1].Value;
            var thenValue = match.Groups[2].Value;
            var elseValue = match.Groups[3].Value;

            // Replace column references in condition
            condition = ReplaceColumnReferences(condition, alias);
            thenValue = ReplaceColumnReferences(thenValue, alias);
            elseValue = ReplaceColumnReferences(elseValue, alias);

            return $"CASE WHEN {condition} THEN {thenValue} ELSE {elseValue} END";
        }

        return $"CASE WHEN {rule} THEN {rule} ELSE NULL END";
    }

    private string ReplaceColumnReferences(string expression, string alias, string addWS = "")
    {
        // Pattern to match table.column references (e.g., tbl.[ColumnName])
        var tableColumnPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\s*\.\s*\[([a-zA-Z0-9_]+)\]";

        // Replace table.column with alias.column
        var result = Regex.Replace(expression, tableColumnPattern, match =>
        {
            var columnName = match.Groups[2].Value;
            return $"{addWS}{alias}.[{columnName}]";
        }, RegexOptions.IgnoreCase);

        // Pattern to match bare bracketed columns (e.g., [ColumnName])
        var bracketedColumnPattern = @"(?<![a-zA-Z0-9_]\s*\.)\s*\[([a-zA-Z0-9_]+)\]";

        result = Regex.Replace(result, bracketedColumnPattern, match =>
        {
            var columnName = match.Groups[1].Value;

            // Don't replace if it's part of an alias we just added
            if (match.Index > 0 && result.Substring(0, match.Index).TrimEnd().EndsWith("."))
                return match.Value;

            // Don't replace if it's in an IN clause (likely a literal value)
            var beforeMatch = match.Index > 10 ? result.Substring(Math.Max(0, match.Index - 10), 10) : result.Substring(0, match.Index);
            if (beforeMatch.Contains("IN", StringComparison.OrdinalIgnoreCase))
                return match.Value;

            return $"{addWS}{alias}.[{columnName}]";
        }, RegexOptions.IgnoreCase);

        return result;
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
