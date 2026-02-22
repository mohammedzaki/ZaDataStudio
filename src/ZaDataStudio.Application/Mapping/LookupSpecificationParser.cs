using System.Text.RegularExpressions;

namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// Helper class to parse and handle lookup table specifications
/// Format: [TableName].[ColumnName] = Value
/// Example: [LookupValues].[LookupTypeId] = 1600
/// </summary>
public class LookupSpecificationParser
{
    /// <summary>
    /// Parse lookup specification from string
    /// </summary>
    public static LookupTableSpec? Parse(string? specification)
    {
        if (string.IsNullOrWhiteSpace(specification))
            return null;

        // Pattern: [EnValueColumnName,ArValueColumnName].[TableName].[ColumnName] = Value
        // Optional: ON [JoinColumnName]
        // Optional: WHERE [condition]
        // Example: [ValueColumnName].[TableName].[ColumnName] = Value ON [JoinColumnName] WHERE condition
        var pattern = @"\[(.+?)(?:,(.+?))?\]\.\[([^\]]+)\](?:\.\[([^\]]+)\]\s*(!=|=)\s*(.+?))?(?:\s+ON\s+\[([^\]]+)\])?(?:\s+WHERE\s+(.+))?$";
        var match = Regex.Match(specification.Trim(), pattern);

        if (match.Success)
        {
            return new LookupTableSpec
            {
                EnValueColumnName = match.Groups[1].Value.Trim(),
                ArValueColumnName = match.Groups[2].Value.Trim(),
                TableName       = match.Groups[3].Success ? match.Groups[3].Value.Trim() : string.Empty,
                ColumnName      = match.Groups.Count > 4 && match.Groups[4].Success ? match.Groups[4].Value.Trim() : string.Empty,
                FilterOperator  = match.Groups.Count > 5 && match.Groups[5].Success ? match.Groups[5].Value.Trim() : string.Empty,
                FilterValue     = match.Groups.Count > 6 && match.Groups[6].Success ? match.Groups[6].Value.Trim() : string.Empty,
                JoinColumnName  = match.Groups.Count > 7 && match.Groups[7].Success ? match.Groups[7].Value.Trim() : string.Empty,
                WhereCondition  = match.Groups.Count > 8 && match.Groups[8].Success ? match.Groups[8].Value.Trim() : string.Empty,
                RawSpecification = specification
            };
        }

        return null;
    }

    /// <summary>
    /// Generate SQL query to get values from lookup table
    /// </summary>
    public static string GenerateLookupQuery(LookupTableSpec spec, string? valueColumn = null, string? additionalWhere = null)
    {
        var tableName = FormatTableName(spec.TableName);
        var query = $"SELECT * FROM {tableName} {(!string.IsNullOrEmpty(spec.ColumnName) ? $"WHERE [{spec.ColumnName}] {spec.FilterOperator} {spec.FilterValue}" : "") }";
        
        if (!string.IsNullOrWhiteSpace(spec.WhereCondition))
        {
            query += $" AND {spec.WhereCondition}";
        }
        
        if (!string.IsNullOrWhiteSpace(additionalWhere))
        {
            query += $" AND {additionalWhere}";
        }

        return query;
    }

    /// <summary>
    /// Format table name with proper brackets and schema
    /// </summary>
    private static string FormatTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return tableName;

        // Already formatted
        if (tableName.StartsWith("[") && tableName.Contains("].["))
            return tableName;

        // Add schema if missing
        if (!tableName.Contains("."))
            tableName = $"dbo.{tableName}";

        // Add brackets
        var parts = tableName.Replace("[", "").Replace("]", "").Split('.');
        return string.Join(".", parts.Select(p => $"[{p}]"));
    }
}

/// <summary>
/// Represents a parsed lookup table specification
/// </summary>
public class LookupTableSpec
{
    public string EnValueColumnName { get; set; } = string.Empty;
    public string ArValueColumnName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string FilterOperator { get; set; } = string.Empty;
    public string FilterValue { get; set; } = string.Empty;
    public string JoinColumnName { get; set; } = string.Empty;
    public string WhereCondition { get; set; } = string.Empty;
    public string RawSpecification { get; set; } = string.Empty;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(EnValueColumnName) &&
        !string.IsNullOrWhiteSpace(TableName) && 
        !string.IsNullOrWhiteSpace(ColumnName) &&
        !string.IsNullOrWhiteSpace(FilterOperator) &&
        !string.IsNullOrWhiteSpace(FilterValue);

    public override string ToString() => RawSpecification;
}
