using System.Text.RegularExpressions;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping.Rules;

/// <summary>
/// Rule 4: Handle SQL expressions (formulas, complex calculations, etc.)
/// Priority: 4 (after Substring, before Concatenation)
/// Prefix: "exp:" or "expression:"
/// Examples:
/// - exp: Amount * 1.15
/// - exp: CAST(OldDate AS DATE)
/// - expression: ISNULL(Field1, 0) + ISNULL(Field2, 0)
/// </summary>
public class ExpressionMappingRule : IMappingRule
{
    public bool CanHandle(DataColumnMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.MappingRule))
            return false;

        var rule = mapping.MappingRule.Trim();

        // Check if mapping rule starts with "exp:" or "expression:" prefix
        return rule.StartsWith("exp:", StringComparison.OrdinalIgnoreCase) ||
               rule.StartsWith("expression:", StringComparison.OrdinalIgnoreCase);
    }

    public MappingResult Apply(DataColumnMapping mapping, MappingContext context)
    {
        var rule = mapping.MappingRule.Trim();

        // Remove the prefix
        string expression;
        if (rule.StartsWith("exp:", StringComparison.OrdinalIgnoreCase))
        {
            expression = rule.Substring(4).Trim();
        }
        else if (rule.StartsWith("expression:", StringComparison.OrdinalIgnoreCase))
        {
            expression = rule.Substring(11).Trim();
        }
        else
        {
            expression = rule;
        }

        // Replace column references with aliased versions
        expression = ReplaceColumnReferences(expression, mapping.OldTableName);

        // Validate brackets
        var openCount = expression.Count(c => c == '(');
        var closeCount = expression.Count(c => c == ')');

        var fullExpression = $"SELECT DISTINCT {expression} AS [{mapping.NewColumn}] FROM {FormatTableName(mapping.OldTableName)}";

        return new MappingResult
        {
            SqlExpression = expression,
            FullSqlExpression = fullExpression,
            HasWarning = openCount != closeCount,
            Warning = openCount != closeCount 
                ? "Expression may have mismatched parentheses" 
                : string.Empty,
            MappingRuleType = nameof(ExpressionMappingRule)
        };
    }

    private string ReplaceColumnReferences(string expression, string tableName)
    {
        var alias = GetTableAlias(tableName);

        // Pattern to match bare column names (not already aliased)
        var columnPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";

        var result = Regex.Replace(expression, columnPattern, match =>
        {
            var word = match.Value;

            // Don't replace SQL keywords
            var sqlKeywords = new[] { 
                "CAST", "CONVERT", "SUBSTRING", "LEFT", "RIGHT", "UPPER", "LOWER",
                "TRIM", "LTRIM", "RTRIM", "LEN", "CHARINDEX", "PATINDEX",
                "ISNULL", "COALESCE", "NULLIF", "CASE", "WHEN", "THEN", "ELSE", "END",
                "AS", "FROM", "WHERE", "AND", "OR", "IS", "NULL", "NOT", "IN",
                "CONCAT", "REPLACE", "REVERSE", "REPLICATE", "STUFF",
                "GETDATE", "DATEADD", "DATEDIFF", "YEAR", "MONTH", "DAY",
                "ABS", "ROUND", "CEILING", "FLOOR", "POWER", "SQRT",
                "SUM", "AVG", "COUNT", "MIN", "MAX",
                "INT", "BIGINT", "DECIMAL", "NUMERIC", "FLOAT", "REAL",
                "VARCHAR", "NVARCHAR", "CHAR", "NCHAR", "TEXT", "NTEXT",
                "DATE", "DATETIME", "DATETIME2", "TIME", "SMALLDATETIME",
                "BIT", "BINARY", "VARBINARY", "IMAGE", "UNIQUEIDENTIFIER"
            };

            if (sqlKeywords.Contains(word.ToUpper()))
                return word;

            // Don't replace numbers
            if (int.TryParse(word, out _) || decimal.TryParse(word, out _))
                return word;

            // Don't replace if already aliased (preceded by dot)
            if (match.Index > 0 && expression[match.Index - 1] == '.')
                return word;

            // Don't replace if it's inside quotes
            var beforeMatch = expression.Substring(0, match.Index);
            var singleQuotes = beforeMatch.Count(c => c == '\'');
            if (singleQuotes % 2 != 0) // Odd number of quotes means we're inside a string
                return word;

            // Add alias
            return $"{alias}.[{word}]";
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
