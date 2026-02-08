using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping.Rules;

/// <summary>
/// Rule 1: Handle NULL or N/A mappings
/// Priority: Highest (1)
/// </summary>
public class NullMappingRule : IMappingRule
{
    public int Priority => 1;

    public bool CanHandle(DataColumnMapping mapping)
    {
        return string.IsNullOrWhiteSpace(mapping.OldColumn) ||
               mapping.OldColumn.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
               string.IsNullOrWhiteSpace(mapping.OldTableName) ||
               mapping.OldTableName.Equals("N/A", StringComparison.OrdinalIgnoreCase);
    }

    public MappingResult Apply(DataColumnMapping mapping, MappingContext context)
    {
        // Check if there's a default value in notes or mapping rule
        var defaultValue = ExtractDefaultValue(mapping);

        return new MappingResult
        {
            SqlExpression = defaultValue ?? "NULL",
            HasWarning = defaultValue == null && !mapping.NewColumnNullable.GetValueOrDefault(true),
            Warning = defaultValue == null && !mapping.NewColumnNullable.GetValueOrDefault(true) 
                ? $"Column {mapping.NewColumn} is NOT NULL but has no source mapping" 
                : string.Empty,
            MappingRuleType = nameof(NullMappingRule)
        };
    }

    private string? ExtractDefaultValue(DataColumnMapping mapping)
    {
        // Check MappingRule or Notes for default values
        var text = $"{mapping.MappingRule} {mapping.Notes}".ToLower();
        
        // Common patterns
        if (text.Contains("current_timestamp"))
            return "GETDATE()";
        
        if (text.Contains("newid()"))
            return "NEWID()";
        
        if (text.Contains("0") && mapping.NewDataType?.Contains("INT") == true)
            return "0";

        if (text.Contains("true") && mapping.NewDataType?.Contains("bit") == true)
            return "1";

        if (text.Contains("false") && mapping.NewDataType?.Contains("bit") == true)
            return "0";

        if (text.Contains("''") || text.Contains("empty string"))
            return "''";

        if (text.Contains("{}") || text.Contains("empty string"))
            return "'{}'";

        return null;
    }
}
