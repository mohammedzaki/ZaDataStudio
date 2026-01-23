using System.Text;
using System.Text.RegularExpressions;
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
            new NullMappingRule(),
            new ExpressionMappingRule(),
            new LookupMappingRule(),
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
            Warning = $"No rule could handle mapping for {mapping.NewColumn}"
        };
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

/// <summary>
/// Context object passed to mapping rules (Strategy pattern)
/// </summary>
public class MappingContext
{
    public string DestinationTable { get; set; } = string.Empty;
    public List<string> SourceTables { get; set; } = new();
    public List<DataColumnMapping> AllMappings { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result of applying a mapping rule
/// </summary>
public class MappingResult
{
    public string SqlExpression { get; set; } = string.Empty;
    public bool HasWarning { get; set; }
    public string Warning { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// Base interface for mapping rules (Strategy pattern)
/// </summary>
public interface IMappingRule
{
    int Priority { get; }
    bool CanHandle(DataColumnMapping mapping);
    MappingResult Apply(DataColumnMapping mapping, MappingContext context);
}

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
                : string.Empty
        };
    }

    private string? ExtractDefaultValue(DataColumnMapping mapping)
    {
        // Check MappingRule or Notes for default values
        var text = $"{mapping.MappingRule} {mapping.Notes}".ToLower();
        
        // Common patterns
        if (text.Contains("getdate()") || text.Contains("current_timestamp"))
            return "GETDATE()";
        
        if (text.Contains("newid()"))
            return "NEWID()";
        
        if (text.Contains("0") && mapping.NewDataType?.Contains("INT") == true)
            return "0";
        
        if (text.Contains("''") || text.Contains("empty string"))
            return "''";

        return null;
    }
}

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

/// <summary>
/// Rule 3: Handle lookup/reference data mappings
/// Priority: 3
/// </summary>
public class LookupMappingRule : IMappingRule
{
    public int Priority => 3;

    public bool CanHandle(DataColumnMapping mapping)
    {
        return mapping.HasLookup || 
               mapping.MappingRule?.Contains("lookup", StringComparison.OrdinalIgnoreCase) == true;
    }

    public MappingResult Apply(DataColumnMapping mapping, MappingContext context)
    {
        // Extract lookup table info from Notes or MappingRule
        var lookupInfo = ParseLookupInfo(mapping);
        
        if (lookupInfo.IsValid)
        {
            var expression = GenerateLookupSQL(mapping, lookupInfo, context);
            return new MappingResult
            {
                SqlExpression = expression,
                Dependencies = { lookupInfo.LookupTable }
            };
        }

        // Fallback: comment for manual implementation
        return new MappingResult
        {
            SqlExpression = $"/* TODO: Implement lookup for {mapping.OldColumn} */ NULL",
            HasWarning = true,
            Warning = $"Lookup required but details not provided for {mapping.NewColumn}"
        };
    }

    private LookupInfo ParseLookupInfo(DataColumnMapping mapping)
    {
        var info = new LookupInfo();
        var text = $"{mapping.MappingRule} {mapping.Notes} {mapping.NewColumnDescription}";
        
        // Pattern: "Lookup: TableName.ColumnName ON SourceColumn = LookupColumn"
        var pattern = @"lookup:\s*(\w+(?:\.\w+)?)\s*ON\s*(\w+)\s*=\s*(\w+)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            info.LookupTable = match.Groups[1].Value;
            info.SourceColumn = match.Groups[2].Value;
            info.LookupJoinColumn = match.Groups[3].Value;
            info.IsValid = true;
        }

        return info;
    }

    private string GenerateLookupSQL(DataColumnMapping mapping, LookupInfo lookup, MappingContext context)
    {
        var sourceTable = GetSourceTableAlias(mapping, context);
        var lookupAlias = GetTableAlias(lookup.LookupTable);
        
        return $"(SELECT TOP 1 {lookupAlias}.[{mapping.OldColumn}] " +
               $"FROM {lookup.LookupTable} {lookupAlias} " +
               $"WHERE {lookupAlias}.[{lookup.LookupJoinColumn}] = {sourceTable}.[{lookup.SourceColumn}])";
    }

    private string GetSourceTableAlias(DataColumnMapping mapping, MappingContext context)
    {
        return !string.IsNullOrWhiteSpace(mapping.OldTableName) 
            ? GetTableAlias(mapping.OldTableName) 
            : context.SourceTables.Any() 
                ? GetTableAlias(context.SourceTables.First()) 
                : "src";
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

    private class LookupInfo
    {
        public bool IsValid { get; set; }
        public string LookupTable { get; set; } = string.Empty;
        public string SourceColumn { get; set; } = string.Empty;
        public string LookupJoinColumn { get; set; } = string.Empty;
    }
}

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
            SqlExpression = conversionExpr
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

public class ValidationReport
{
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool IsValid => !Errors.Any();

    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Mapping Validation Report ===");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        if (IsValid)
        {
            sb.AppendLine("✓ No errors found");
        }
        else
        {
            sb.AppendLine($"✗ {Errors.Count} error(s) found:");
            foreach (var error in Errors)
            {
                sb.AppendLine($"  - {error}");
            }
        }

        sb.AppendLine();

        if (Warnings.Any())
        {
            sb.AppendLine($"⚠ {Warnings.Count} warning(s):");
            foreach (var warning in Warnings)
            {
                sb.AppendLine($"  - {warning}");
            }
        }

        return sb.ToString();
    }
}

