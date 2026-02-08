using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping.Rules;

/// <summary>
/// Rule 6: Handle type conversion mappings
/// Priority: 6
/// </summary>
public class TypeConversionMappingRule : IMappingRule
{
    public int Priority => 6;

    public bool CanHandle(DataColumnMapping mapping)
    {
        // Check if type conversion is needed
        if (string.IsNullOrWhiteSpace(mapping.OldDataType) || 
            string.IsNullOrWhiteSpace(mapping.NewDataType))
            return false;

        var oldType = mapping.OldDataType.Split('(')[0].Trim().ToUpper();
        var newType = mapping.NewDataType.Split('(')[0].Trim().ToUpper();

        return oldType != newType && RequiresExplicitConversion(oldType, newType);
    }

    public MappingResult Apply(DataColumnMapping mapping, MappingContext context)
    {
        var sourceExpr = GetSourceColumnExpression(mapping);
        var conversionExpr = GenerateConversion(sourceExpr, mapping.OldDataType, mapping.NewDataType);

        return new MappingResult
        {
            SqlExpression = conversionExpr,
            MappingRuleType = nameof(TypeConversionMappingRule)
        };
    }

    private string GetSourceColumnExpression(DataColumnMapping mapping)
    {
        if (!string.IsNullOrWhiteSpace(mapping.OldTableName))
        {
            var alias = GetTableAlias(mapping.OldTableName);
            return $"{alias}.[{mapping.OldColumn}]";
        }
        return $"[{mapping.OldColumn}]";
    }

    private bool RequiresExplicitConversion(string oldType, string newType)
    {
        // Types that need explicit CAST/CONVERT
        var conversionPairs = new[]
        {
            ("VARCHAR", "INT"),
            ("NVARCHAR", "INT"),
            ("VARCHAR", "DATETIME"),
            ("NVARCHAR", "DATETIME"),
            ("INT", "VARCHAR"),
            ("INT", "NVARCHAR"),
            ("DATETIME", "VARCHAR"),
            ("DECIMAL", "INT"),
            ("FLOAT", "INT")
        };

        return conversionPairs.Any(p => p.Item1 == oldType && p.Item2 == newType);
    }

    private string GenerateConversion(string sourceExpr, string oldType, string newType)
    {
        var newTypeBase = newType.Split('(')[0].ToUpper();
        
        // Use TRY_CAST for safer conversions
        if (newTypeBase == "INT" || newTypeBase == "BIGINT" || newTypeBase == "DECIMAL")
        {
            return $"TRY_CAST({sourceExpr} AS {newType.ToUpper()})";
        }

        // Use TRY_CONVERT for datetime
        if (newTypeBase == "DATETIME" || newTypeBase == "DATETIME2" || newTypeBase == "DATE")
        {
            return $"TRY_CONVERT({newType.ToUpper()}, {sourceExpr}, 120)";
        }

        // Standard CAST for string types
        return $"CAST({sourceExpr} AS {newType.ToUpper()})";
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
}
