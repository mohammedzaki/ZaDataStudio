using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping.Rules;

/// <summary>
/// Rule 4: Handle concatenation mappings
/// Priority: 4
/// Pattern: "Column1 + Column2" or "CONCAT(Column1, Column2)"
/// </summary>
public class ConcatenationMappingRule : IMappingRule
{
    public int Priority => 4;

    public bool CanHandle(DataColumnMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.MappingRule))
            return false;

        return mapping.MappingRule.Contains("+", StringComparison.Ordinal) ||
               mapping.MappingRule.Contains("concat", StringComparison.OrdinalIgnoreCase) ||
               mapping.MappingRule.Contains("&");
    }

    public MappingResult Apply(DataColumnMapping mapping, MappingContext context)
    {
        // Parse concatenation rule
        var expression = ParseConcatenation(mapping.MappingRule, mapping.OldTableName);
        
        return new MappingResult
        {
            SqlExpression = expression
        };
    }

    private string ParseConcatenation(string rule, string tableName)
    {
        // Replace column references with properly formatted table.column
        var parts = rule.Split('+');
        var formattedParts = parts.Select(p => 
        {
            var col = p.Trim();
            if (col.StartsWith("'") && col.EndsWith("'"))
                return col; // Keep string literals as-is
            
            return $"[{col}]";
        });

        return $"CONCAT({string.Join(", ", formattedParts)})";
    }
}
