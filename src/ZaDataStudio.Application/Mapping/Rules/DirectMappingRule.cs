using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping.Rules;

/// <summary>
/// Rule 7: Direct column mapping (default)
/// Priority: Lowest (7)
/// </summary>
public class DirectMappingRule : IMappingRule
{
    public int Priority => 7;

    public bool CanHandle(DataColumnMapping mapping)
    {
        // This is the default rule - always returns true
        return !string.IsNullOrWhiteSpace(mapping.OldColumn);
    }

    public MappingResult Apply(DataColumnMapping mapping, MappingContext context)
    {
        var sourceExpr = GenerateSourceExpression(mapping);
        
        return new MappingResult
        {
            SqlExpression = sourceExpr
        };
    }

    private string GenerateSourceExpression(DataColumnMapping mapping)
    {
        if (!string.IsNullOrWhiteSpace(mapping.OldTableName))
        {
            var alias = GetTableAlias(mapping.OldTableName);
            return $"{alias}.[{mapping.OldColumn}]";
        }
        
        return $"[{mapping.OldColumn}]";
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
