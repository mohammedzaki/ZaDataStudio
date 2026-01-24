using System.Text.RegularExpressions;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping.Rules;

/// <summary>
/// Rule 5: Handle conditional mappings (CASE WHEN)
/// Priority: 5
/// Pattern: "IF condition THEN value1 ELSE value2"
/// </summary>
public class ConditionalMappingRule : IMappingRule
{
    public int Priority => 5;

    public bool CanHandle(DataColumnMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.MappingRule))
            return false;

        return mapping.MappingRule.Contains("IF", StringComparison.OrdinalIgnoreCase) ||
               mapping.MappingRule.Contains("WHEN", StringComparison.OrdinalIgnoreCase) ||
               mapping.MappingRule.Contains("?");
    }

    public MappingResult Apply(DataColumnMapping mapping, MappingContext context)
    {
        var expression = ConvertToCase(mapping.MappingRule, mapping.OldTableName);
        
        return new MappingResult
        {
            SqlExpression = expression,
            HasWarning = !expression.Contains("END"),
            Warning = !expression.Contains("END") ? "Conditional expression may be incomplete" : string.Empty
        };
    }

    private string ConvertToCase(string rule, string tableName)
    {
        // Simple IF...THEN...ELSE conversion
        var pattern = @"IF\s+(.+?)\s+THEN\s+(.+?)\s+ELSE\s+(.+)";
        var match = Regex.Match(rule, pattern, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            var condition = match.Groups[1].Value;
            var thenValue = match.Groups[2].Value;
            var elseValue = match.Groups[3].Value;
            
            return $"CASE WHEN {condition} THEN {thenValue} ELSE {elseValue} END";
        }

        // Already a CASE statement
        if (rule.Contains("CASE", StringComparison.OrdinalIgnoreCase))
        {
            return rule;
        }

        return $"CASE WHEN {rule} THEN {rule} ELSE NULL END";
    }
}
