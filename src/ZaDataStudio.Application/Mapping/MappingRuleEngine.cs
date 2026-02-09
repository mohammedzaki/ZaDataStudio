using System.Text;
using ZaDataStudio.Application.Mapping.Rules;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// Advanced mapping rule engine using Strategy and Chain of Responsibility patterns
/// Handles complex data transformation rules from Excel mapping configurations
/// </summary>
public class MappingRuleEngine
{
    private readonly List<IMappingRule> _rules;

    public MappingRuleEngine()
    {
        // Initialize rules in priority order (Chain of Responsibility)
        _rules =
        [
            new ColumnToRowMappingRule(),
            new LookupMappingRule(),
            new NullMappingRule(),
            new ExpressionMappingRule(),
            new ConcatenationMappingRule(),
            new ConditionalMappingRule(),
            new TypeConversionMappingRule(),
            new DirectMappingRule()
        ];
    }

    /// <summary>
    /// Process a column mapping and generate appropriate SQL expression
    /// </summary>
    public MappingResult ProcessMapping(DataColumnMapping mapping, MappingContext context)
    {
        // Try each rule in priority order
        foreach (var rule in _rules)
        {
            if (rule.CanHandle(mapping))
            {
                return rule.Apply(mapping, context);
            }
        }

        // Fallback
        return new MappingResult
        {
            SqlExpression = "NULL",
            HasWarning = true,
            Warning = $"No rule could handle mapping for {mapping.NewColumn}",
            MappingRuleType = "NoRuleMatched"
        };
    }

    /// <summary>
    /// Process a column mapping and generate appropriate SQL expression
    /// </summary>
    public string GenerateMappingRuleSQL(DataColumnMapping mapping)
    {
        try 
        {
            var mappingResult = ProcessMapping(mapping, new MappingContext());
            return mappingResult.FullSqlExpression;
        }
        catch (Exception ex)
        {
            return $"-- ERROR generating mapping rule SQL for {mapping.NewColumn}: {ex.Message}";
        }
    }

    /// <summary>
    /// Generate complete migration SQL from configuration
    /// </summary>
    public string GenerateMigrationSQL(DataMappingConfiguration config, bool includeTransaction = true)
    {
        var sql = new StringBuilder();
        
        // Header
        sql.AppendLine("-- =====================================================");
        sql.AppendLine("-- Advanced Data Migration SQL");
        sql.AppendLine($"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sql.AppendLine($"-- Total Tables: {config.GroupedByTable.Count}");
        sql.AppendLine($"-- Total Columns: {config.ColumnMappings.Count}");
        sql.AppendLine("-- =====================================================");
        sql.AppendLine();

        if (includeTransaction)
        {
            sql.AppendLine("BEGIN TRANSACTION;");
            sql.AppendLine();
        }

        // Process each table
        foreach (var tableGroup in config.GroupedByTable.OrderBy(t => t.Key))
        {
            var context = new MappingContext
            {
                DestinationTable = tableGroup.Key,
                AllMappings = tableGroup.Value
            };

            var tableResult = GenerateTableMigrationSQL(tableGroup.Key, tableGroup.Value, context);
            sql.AppendLine(tableResult);
        }

        if (includeTransaction)
        {
            sql.AppendLine("-- Review the above statements before committing");
            sql.AppendLine("-- COMMIT TRANSACTION;");
            sql.AppendLine("-- ROLLBACK TRANSACTION; -- Uncomment to undo changes");
        }

        sql.AppendLine();
        sql.AppendLine("-- =====================================================");
        sql.AppendLine("-- End of Migration SQL");
        sql.AppendLine("-- =====================================================");

        return sql.ToString();
    }

    private string GenerateTableMigrationSQL(string tableName, List<DataColumnMapping> mappings, MappingContext context)
    {
        var sql = new StringBuilder();
        var rowsToColmunsPart = new StringBuilder();

        // Filter approved mappings
        var approvedMappings = mappings
            .Where(m => string.IsNullOrWhiteSpace(m.MappingStatus) || 
                       m.MappingStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!approvedMappings.Any())
        {
            sql.AppendLine($"-- SKIPPED: {tableName} - No approved mappings");
            sql.AppendLine();
            return sql.ToString();
        }

        // Analyze source tables
        var sourceTables = approvedMappings
            .Where(m => !string.IsNullOrWhiteSpace(m.OldTableName) && 
                       !m.OldTableName.Equals("N/A", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.OldTableName)
            .Distinct()
            .ToList();

        context.SourceTables = sourceTables;

        // Build SQL
        sql.AppendLine($"-- ============================================");
        sql.AppendLine($"-- Table: {tableName}");
        sql.AppendLine($"-- Source Tables: {(sourceTables.Any() ? string.Join(", ", sourceTables) : "None (static values)")}");
        sql.AppendLine($"-- Columns: {approvedMappings.Count}");
        sql.AppendLine($"-- ============================================");
        sql.AppendLine();

        // Generate INSERT
        var destColumns = approvedMappings.Select(m => $"[{m.NewColumn}]").ToList();
        
        sql.AppendLine($"INSERT INTO {FormatTableName(tableName)}");
        sql.AppendLine($"    ({string.Join(", ", destColumns)})");
        sql.AppendLine("SELECT");

        // Process each column mapping
        var selectExpressions = new List<string>();
        var warnings = new List<string>();
        foreach (var mapping in approvedMappings)
        {
            var result = ProcessMapping(mapping, context);
            if (result.MappingRuleType == nameof(ColumnToRowMappingRule))
                rowsToColmunsPart.Append(result.FullSqlExpression);
            selectExpressions.Add($"    {result.SqlExpression} AS [{mapping.NewColumn}]");
            
            if (result.HasWarning)
            {
                warnings.Add($"-- WARNING [{mapping.NewColumn}]: {result.Warning}");
            }
        }

        sql.AppendLine(string.Join($",{Environment.NewLine}", selectExpressions));

        // FROM clause
        if (sourceTables.Any())
        {
            var primaryTable = sourceTables.First();
            sql.AppendLine($"FROM {FormatTableName(primaryTable)} AS {GetTableAlias(primaryTable)}");

            // Add JOINs for additional tables
            for (int i = 1; i < sourceTables.Count; i++)
            {
                var joinTable = sourceTables[i];
                sql.AppendLine($"    -- TODO: Define JOIN condition for {joinTable}");
                sql.AppendLine($"    LEFT JOIN {FormatTableName(joinTable)} AS {GetTableAlias(joinTable)}");
                sql.AppendLine($"        ON {GetTableAlias(primaryTable)}.KeyColumn = {GetTableAlias(joinTable)}.KeyColumn");
            }
        }

        // Add duplicate prevention
        var keyColumns = approvedMappings
            .Where(m => m.NewColumn.Contains("Id") || m.NewColumn.Contains("Key"))
            .Take(1)
            .ToList();

        if (keyColumns.Any())
        {
            var keyCol = keyColumns.First();
            sql.AppendLine("WHERE NOT EXISTS (");
            sql.AppendLine($"    SELECT 1 FROM {FormatTableName(tableName)} dest");
            sql.AppendLine($"    WHERE dest.[{keyCol.NewColumn}] = {GetSourceExpression(keyCol)}");
            sql.AppendLine(");");
        }
        else
        {
            sql.AppendLine(";");
        }

        sql.AppendLine();
        sql.AppendLine(rowsToColmunsPart.ToString());
        rowsToColmunsPart.Clear();

        sql.AppendLine();
        sql.AppendLine($"-- Records inserted: @@ROWCOUNT");
        sql.AppendLine();

        // Add warnings
        if (warnings.Any())
        {
            sql.AppendLine("-- WARNINGS:");
            foreach (var warning in warnings)
            {
                sql.AppendLine(warning);
            }
            sql.AppendLine();
        }

        return sql.ToString();
    }

    private string GetSourceExpression(DataColumnMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.OldColumn) || 
            mapping.OldColumn.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return "NULL";
        }

        if (!string.IsNullOrWhiteSpace(mapping.OldTableName))
        {
            return $"{GetTableAlias(mapping.OldTableName)}.[{mapping.OldColumn}]";
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
