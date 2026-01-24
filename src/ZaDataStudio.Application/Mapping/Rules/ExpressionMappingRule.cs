using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping.Rules;

/// <summary>
/// Rule 2: Handle SQL expressions (formulas, CASE statements, etc.)
/// Priority: 2
/// </summary>
public class ExpressionMappingRule : IMappingRule
{
    public int Priority => 2;

    public bool CanHandle(DataColumnMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.OldColumn))
            return false;

        // Check for SQL expression indicators
        return mapping.OldColumn.Contains("(") ||
               mapping.OldColumn.Contains("+") ||
               mapping.OldColumn.Contains("CASE") ||
               mapping.OldColumn.Contains("CAST") ||
               mapping.OldColumn.Contains("CONVERT") ||
               mapping.OldColumn.Contains("CONCAT") ||
               mapping.OldColumn.Contains("ISNULL") ||
               mapping.OldColumn.Contains("COALESCE");
    }

    public MappingResult Apply(DataColumnMapping mapping, MappingContext context)
    {
        // Expression is already SQL - use as-is with validation
        var expression = mapping.OldColumn.Trim();
        
        // Validate brackets
        var openCount = expression.Count(c => c == '(');
        var closeCount = expression.Count(c => c == ')');
        
        return new MappingResult
        {
            SqlExpression = expression,
            HasWarning = openCount != closeCount,
            Warning = openCount != closeCount 
                ? "Expression may have mismatched parentheses" 
                : string.Empty
        };
    }
}
