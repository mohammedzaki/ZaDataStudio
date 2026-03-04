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
        var pattern1 = @"\[(.+?)(?:,(.+?))?\]\.\[([^\]]+)\](?:\.\[([^\]]+)\]\s*(!=|=)\s*(.+?))?(?:\s+ON\s+\[([^\]]+)\])?(?:\s+WHERE\s+(.+))?$";
        var match = Regex.Match(specification.Trim(), pattern1);

        if (match.Success)
        {
            return new LookupTableSpec
            {
                EnValueColumnName = match.Groups[1].Value.Trim(),
                ArValueColumnName = match.Groups[2].Value.Trim(),
                TableName = match.Groups[3].Success ? match.Groups[3].Value.Trim() : string.Empty,
                ColumnName = match.Groups.Count > 4 && match.Groups[4].Success ? match.Groups[4].Value.Trim() : string.Empty,
                FilterOperator = match.Groups.Count > 5 && match.Groups[5].Success ? match.Groups[5].Value.Trim() : string.Empty,
                FilterValue = match.Groups.Count > 6 && match.Groups[6].Success ? match.Groups[6].Value.Trim() : string.Empty,
                JoinColumnName = match.Groups.Count > 7 && match.Groups[7].Success ? match.Groups[7].Value.Trim() : string.Empty,
                WhereCondition = match.Groups.Count > 8 && match.Groups[8].Success ? match.Groups[8].Value.Trim() : string.Empty,
                RawSpecification = specification,
                LookupTableSpecType = LookupTableSpecType.Simple
            };
        }

        // Pattern: [EnValueColumnName,ArValueColumnName] 1,2 FROM [TableName] 3  
        // Optional: JOIN [JoinTable] 4 ON [TableNameColumnName] 5 = [JoinTableColumnName] 6
        // Optional: WHERE [condition] 7
        // Example: [EnValueColumnName,ArValueColumnName] FROM [TableName] JOIN [JoinTable] ON [TableNameColumnName] = [JoinTableColumnName] WHERE [ColumnName] = Value
        var pattern2 = @"\[(.+?)(?:,(.+?))?\]\s+FROM\s+\[(.+?)\](?:\s+JOIN\s+\[(.+?)\]\s+ON\s+\[(.+?)\]\s+=\s+\[(.+?)\])?\s+WHERE\s+(.+?)$";
        var matchPattern2 = Regex.Match(specification.Trim(), pattern2);
        if (matchPattern2.Success)
        { 
            return new LookupTableSpec
            {
                EnValueColumnName = matchPattern2.Groups[1].Value.Trim(),
                ArValueColumnName = matchPattern2.Groups[2].Value.Trim(),
                TableName = matchPattern2.Groups[3].Success ? matchPattern2.Groups[3].Value.Trim() : string.Empty,
                JoinTable = matchPattern2.Groups.Count > 4 && matchPattern2.Groups[4].Success ? matchPattern2.Groups[4].Value.Trim() : string.Empty,
                ColumnName = matchPattern2.Groups.Count > 5 && matchPattern2.Groups[5].Success ? matchPattern2.Groups[5].Value.Trim() : string.Empty,
                JoinColumnName = matchPattern2.Groups.Count > 6 && matchPattern2.Groups[6].Success ? matchPattern2.Groups[6].Value.Trim() : string.Empty,
                WhereCondition = matchPattern2.Groups.Count > 7 && matchPattern2.Groups[7].Success ? matchPattern2.Groups[7].Value.Trim() : string.Empty,
                RawSpecification = specification,
                LookupTableSpecType = LookupTableSpecType.Join
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
    /// Generate SQL query to get values from lookup table
    /// </summary>
    public static string GenerateLookupSqlExpression(LookupTableSpec spec)
    {
        var tableName = FormatTableName(spec.TableName);
        var query = "";
        switch (spec.LookupTableSpecType)
        {
            default:
            case LookupTableSpecType.Simple:
                query = $@"
            SELECT DISTINCT [{spec.JoinColumnName}] AS LookupCode, 
            [{spec.EnValueColumnName}] AS LookupEnValue
            {(string.IsNullOrEmpty(spec.ArValueColumnName) ? "" : $",[{spec.ArValueColumnName}] AS LookupArValue")}
            FROM {tableName}
            {(!string.IsNullOrEmpty(spec.ColumnName) ? $"WHERE [{spec.ColumnName}] {spec.FilterOperator} {spec.FilterValue}" : "")}
            ORDER BY [{spec.JoinColumnName}]";
                break;
            case LookupTableSpecType.Join:
                query = $@"
            SELECT DISTINCT {tableName}.[{spec.JoinColumnName}] AS LookupCode, 
            {tableName}.[{spec.EnValueColumnName}] AS LookupEnValue
            {(string.IsNullOrEmpty(spec.ArValueColumnName) ? "" : $",{tableName}.[{spec.ArValueColumnName}] AS LookupArValue")}
            FROM {tableName}
            {(!string.IsNullOrEmpty(spec.JoinTable) ? $"INNER JOIN [{spec.JoinTable}] ON [{spec.JoinTable}].[{spec.JoinColumnName}] = {tableName}.[{spec.ColumnName}]" : "")}
            WHERE {spec.WhereCondition}
            ORDER BY {tableName}.[{spec.JoinColumnName}]";
                break;
        }
        return query;
    }

    /// <summary>
    /// Generate SQL query to get values from lookup table
    /// </summary>
    public static string GenerateLookupSqlCountQuery(LookupTableSpec spec, string sourceTableName, string sourceColumnName)
    {
        var joinStr = @$"LEFT JOIN {spec.TableName} ON {spec.TableName}.{spec.JoinColumnName} = {sourceTableName}.{sourceColumnName}";
        if (sourceTableName == spec.TableName)
            joinStr = "";
        var query = $@"
                    SELECT [{sourceColumnName}], [{spec.EnValueColumnName}], COUNT(*) as RecordCount
                    FROM {sourceTableName}
                    {joinStr}
                    GROUP BY [{sourceColumnName}], [{spec.EnValueColumnName}]
                    ORDER BY RecordCount DESC";
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
    public string JoinTable { get; set; } = string.Empty;
    public string JoinColumnName { get; set; } = string.Empty;
    public string WhereCondition { get; set; } = string.Empty;
    public string RawSpecification { get; set; } = string.Empty;
    public LookupTableSpecType LookupTableSpecType { get; set; } = LookupTableSpecType.Simple;
    public override string ToString() => $"{LookupTableSpecType}:{RawSpecification}";
}

public enum LookupTableSpecType
{
    Simple = 1,
    Join = 2
}