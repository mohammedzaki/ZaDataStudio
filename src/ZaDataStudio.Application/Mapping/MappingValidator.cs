using System.Text;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// Validation service for mapping configurations
/// </summary>
public class MappingValidator
{
    public ValidationReport Validate(DataMappingConfiguration config)
    {
        var report = new ValidationReport();

        foreach (var mapping in config.ColumnMappings)
        {
            ValidateMapping(mapping, report);
        }

        // Cross-table validations
        ValidateTableRelationships(config, report);
        ValidateNullability(config, report);

        return report;
    }

    private void ValidateMapping(DataColumnMapping mapping, ValidationReport report)
    {
        // Check required fields
        if (string.IsNullOrWhiteSpace(mapping.NewTableName))
        {
            report.Errors.Add($"Missing destination table name for column {mapping.NewColumn}");
        }

        if (string.IsNullOrWhiteSpace(mapping.NewColumn))
        {
            report.Errors.Add($"Missing destination column name in table {mapping.NewTableName}");
        }

        // Check type compatibility
        if (!string.IsNullOrWhiteSpace(mapping.OldDataType) && 
            !string.IsNullOrWhiteSpace(mapping.NewDataType))
        {
            if (!AreTypesCompatible(mapping.OldDataType, mapping.NewDataType))
            {
                report.Warnings.Add($"{mapping.NewTableName}.{mapping.NewColumn}: Type conversion needed from {mapping.OldDataType} to {mapping.NewDataType}");
            }
        }

        // Check NULL constraints
        if (mapping.NewColumnNullable == false && 
            (string.IsNullOrWhiteSpace(mapping.OldColumn) || 
             mapping.OldColumn.Equals("N/A", StringComparison.OrdinalIgnoreCase)))
        {
            report.Errors.Add($"{mapping.NewTableName}.{mapping.NewColumn}: NOT NULL column has no source mapping");
        }

        // Check lookup configuration
        if (mapping.HasLookup && string.IsNullOrWhiteSpace(mapping.MappingRule))
        {
            report.Warnings.Add($"{mapping.NewTableName}.{mapping.NewColumn}: Lookup marked but no mapping rule provided");
        }
    }

    private void ValidateTableRelationships(DataMappingConfiguration config, ValidationReport report)
    {
        foreach (var tableGroup in config.GroupedByTable)
        {
            var sourceTables = tableGroup.Value
                .Where(m => !string.IsNullOrWhiteSpace(m.OldTableName) && 
                           !m.OldTableName.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.OldTableName)
                .Distinct()
                .ToList();

            if (sourceTables.Count > 1)
            {
                var hasJoinKeys = tableGroup.Value.Any(m => 
                    m.NewColumn.Contains("Id", StringComparison.OrdinalIgnoreCase) ||
                    m.NewColumn.Contains("Key", StringComparison.OrdinalIgnoreCase));

                if (!hasJoinKeys)
                {
                    report.Warnings.Add($"{tableGroup.Key}: Multiple source tables ({string.Join(", ", sourceTables)}) but no obvious join keys defined");
                }
            }
        }
    }

    private void ValidateNullability(DataMappingConfiguration config, ValidationReport report)
    {
        var nullableIssues = config.ColumnMappings
            .Where(m => m.NewColumnNullable == false && 
                       m.OldColumnNullable == true)
            .ToList();

        foreach (var issue in nullableIssues)
        {
            report.Warnings.Add($"{issue.NewTableName}.{issue.NewColumn}: Source allows NULL but destination does not");
        }
    }

    private bool AreTypesCompatible(string oldType, string newType)
    {
        var oldBase = oldType.Split('(')[0].ToUpper().Trim();
        var newBase = newType.Split('(')[0].ToUpper().Trim();

        if (oldBase == newBase)
            return true;

        var compatiblePairs = new[]
        {
            ("INT", "BIGINT"),
            ("SMALLINT", "INT"),
            ("TINYINT", "SMALLINT"),
            ("VARCHAR", "NVARCHAR"),
            ("CHAR", "VARCHAR"),
            ("DATE", "DATETIME"),
            ("DATETIME2", "DATETIME"),
            ("FLOAT", "DECIMAL")
        };

        return compatiblePairs.Any(p => p.Item1 == oldBase && p.Item2 == newBase);
    }
}
