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
            //new LookupMappingRule(),
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
    public string GenerateMigrationSQL(
        DataMappingConfiguration config,
        MappingComparisonResult analysisResult,
        List<DatatypeComparison> datatypeComparisons,
        string sourceDatabase = "",
        string destinationDatabase = "",
        bool includeTransaction = true)
    {
        var sql = new StringBuilder();

        // Header
        sql.AppendLine("-- =====================================================");
        sql.AppendLine("-- Advanced Data Migration SQL");
        sql.AppendLine($"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (!string.IsNullOrWhiteSpace(sourceDatabase))
            sql.AppendLine($"-- Source Database: {sourceDatabase}");
        if (!string.IsNullOrWhiteSpace(destinationDatabase))
            sql.AppendLine($"-- Destination Database: {destinationDatabase}");
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

            var tableResult = GenerateTableMigrationSQL(
                tableGroup.Key, 
                tableGroup.Value, 
                context, 
                analysisResult,
                sourceDatabase,
                destinationDatabase);
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

    private string GenerateTableMigrationSQL(
        string tableName, 
        List<DataColumnMapping> mappings, 
        MappingContext context,
        MappingComparisonResult? analysisResult = null,
        string sourceDatabase = "",
        string destinationDatabase = "")
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

        sql.AppendLine($"INSERT INTO {FormatTableName(tableName, destinationDatabase)}");
        sql.AppendLine($"    ({string.Join(", ", destColumns)})");
        sql.AppendLine("SELECT");

        // Process each column mapping
        var selectExpressions = new List<string>();
        var warnings = new List<string>();

        foreach (var mapping in approvedMappings)
        {
            string sqlExpression;

            // Check if this mapping has lookup analysis with ValuesMapping
            var lookupAnalysis = analysisResult?.LookupAnalysis?.FirstOrDefault(la =>
                la.TableName == mapping.NewTableName && 
                la.ColumnName == mapping.NewColumn &&
                la.ValuesMapping != null && 
                la.ValuesMapping.Any());

            if (lookupAnalysis != null)
            {
                // Generate CASE WHEN statement from ValuesMapping
                sqlExpression = GenerateLookupCaseWhen(mapping, lookupAnalysis, sourceDatabase);
            }
            else
            {
                // Use regular rule engine
                var result = ProcessMapping(mapping, context);

                if (result.MappingRuleType == nameof(ColumnToRowMappingRule))
                {
                    rowsToColmunsPart.Append(result.FullSqlExpression);
                }

                sqlExpression = result.SqlExpression;

                if (result.HasWarning)
                {
                    warnings.Add($"-- WARNING [{mapping.NewColumn}]: {result.Warning}");
                }
            }

            selectExpressions.Add($"    {sqlExpression} AS [{mapping.NewColumn}]");
        }

        sql.AppendLine(string.Join($",{Environment.NewLine}", selectExpressions));

        // FROM clause
        if (sourceTables.Any())
        {
            var primaryTable = sourceTables.First();
            sql.AppendLine($"FROM {FormatTableName(primaryTable, sourceDatabase)} AS {GetTableAlias(primaryTable)}");

            // Add JOINs for additional tables
            for (int i = 1; i < sourceTables.Count; i++)
            {
                var joinTable = sourceTables[i];
                sql.AppendLine($"    -- TODO: Define JOIN condition for {joinTable}");
                sql.AppendLine($"    LEFT JOIN {FormatTableName(joinTable, sourceDatabase)} AS {GetTableAlias(joinTable)}");
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
            sql.AppendLine($"    SELECT 1 FROM {FormatTableName(tableName, destinationDatabase)} dest");
            sql.AppendLine($"    WHERE dest.[{keyCol.NewColumn}] = {GetSourceExpression(keyCol, sourceDatabase)}");
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

    /// <summary>
    /// Generate CASE WHEN statement for lookup mappings using ValuesMapping
    /// </summary>
    private string GenerateLookupCaseWhen(DataColumnMapping mapping, LookupColumnAnalysis lookupAnalysis, string sourceDatabase = "")
    {
        if (lookupAnalysis.ValuesMapping == null || !lookupAnalysis.ValuesMapping.Any())
        {
            return "NULL -- No lookup mappings found";
        }

        var sql = new StringBuilder();
        var sourceExpression = GetSourceExpression(mapping, sourceDatabase);

        sql.AppendLine("CASE");

        // Generate WHEN clauses for matched values
        var matchedMappings = lookupAnalysis.ValuesMapping
            .Where(vm => !string.IsNullOrEmpty(vm.DestinationLookupValue))
            .ToList();

        foreach (var valueMap in matchedMappings)
        {
            // Handle NULL source codes
            if (string.IsNullOrEmpty(valueMap.SourceLookupCode) || 
                valueMap.SourceLookupCode.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            {
                sql.AppendLine($"        WHEN {sourceExpression} IS NULL THEN '{EscapeSql(valueMap.DestinationLookupCode)}'");
            }
            else if (IsNumeric(valueMap.SourceLookupCode))
            {
                sql.AppendLine($"        WHEN {sourceExpression} IN ({valueMap.SourceLookupCode}) THEN '{EscapeSql(valueMap.DestinationLookupCode)}'");
            }
            else
            {
                sql.AppendLine($"        WHEN {sourceExpression} IN ('{EscapeSql(valueMap.SourceLookupCode)}') THEN '{EscapeSql(valueMap.DestinationLookupCode)}'");
            }
        }

        sql.Append("        ELSE NULL -- Unmapped value");

        // Add comment about unmapped values if any exist
        var unmappedCount = lookupAnalysis.ValuesMapping.Count - matchedMappings.Count;
        if (unmappedCount > 0)
        {
            sql.Append($" ({unmappedCount} unmapped value(s))");
        }

        sql.AppendLine();
        sql.Append("    END");

        return sql.ToString();
    }

    /// <summary>
    /// Escape single quotes in SQL string literals
    /// </summary>
    private string EscapeSql(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Replace("'", "''");
    }

    /// <summary>
    /// Check if a string represents a numeric value
    /// </summary>
    private bool IsNumeric(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return int.TryParse(value, out _) || 
               long.TryParse(value, out _) || 
               decimal.TryParse(value, out _);
    }

    private string GetSourceExpression(DataColumnMapping mapping, string sourceDatabase = "")
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

    private string FormatTableName(string tableName, string databaseName = "")
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return tableName;

        // If table name already includes database (3-part name), return as is
        if (tableName.StartsWith("[") && tableName.Split('.').Length >= 3)
            return tableName;

        var cleanName = tableName.Replace("[", "").Replace("]", "");
        var parts = cleanName.Split('.');

        // Build the formatted name
        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            // Include database name: [Database].[Schema].[Table]
            if (parts.Length >= 2)
            {
                // Table name already has schema
                return $"[{databaseName}].[{parts[0]}].[{parts[1]}]";
            }
            else
            {
                // Add default schema (dbo)
                return $"[{databaseName}].[dbo].[{parts[0]}]";
            }
        }
        else
        {
            // No database name provided: [Schema].[Table]
            if (parts.Length >= 2)
            {
                return $"[{parts[0]}].[{parts[1]}]";
            }
            else
            {
                // Add default schema (dbo)
                return $"[dbo].[{parts[0]}]";
            }
        }
    }
}
