using ZaDataStudio.Application.Common.Interfaces;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// Analyzes lookup column mappings and compares source/destination values
/// </summary>
public class LookupColumnAnalyzer : ILookupColumnAnalyzer
{
    private readonly IDatabaseService _databaseService;
    private readonly MappingRuleEngine _ruleEngine;

    public LookupColumnAnalyzer(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
        _ruleEngine = new MappingRuleEngine();
    }

    /// <summary>
    /// Analyze lookup column using mapping rule
    /// </summary>
    public async Task<LookupColumnAnalysis> AnalyzeLookupColumnAsync(
        DataColumnMapping columnMapping,
        string sourceConnectionString,
        string destinationConnectionString)
    {
        var analysis = new LookupColumnAnalysis();

        // Load source data using database service - connection will be reused
        var sqlExpression = "";
        if (!string.IsNullOrWhiteSpace(columnMapping.MappingRule))
        {
            sqlExpression = _ruleEngine.GenerateMappingRuleSQL(columnMapping);
        }
        if (string.IsNullOrWhiteSpace(sqlExpression))
        {
            // Get distinct values from source
            sqlExpression = $@"
                SELECT DISTINCT [{columnMapping.OldColumn}], [{columnMapping.OldColumn}]
                FROM {columnMapping.OldTableName} 
                ORDER BY [{columnMapping.OldColumn}]";
        }

        using var srcReader = await _databaseService.ExecuteReaderAsync(sourceConnectionString, sqlExpression);
        while (await srcReader.ReadAsync())
        {
            analysis.SourceSampleValues.Add(srcReader[0]?.ToString() ?? "", srcReader[1]?.ToString() ?? "");
        }
        await srcReader.DisposeAsync();

        // Get distinct count - reuses same connection
        var countQuery = $"SELECT COUNT(DISTINCT [{columnMapping.OldColumn}]) FROM {columnMapping.OldTableName}";
        var countResult = await _databaseService.ExecuteScalarAsync(sourceConnectionString, countQuery);
        analysis.SourceDistinctCount = countResult != null ? Convert.ToInt32(countResult) : 0;
        analysis.SourceLookupQuery = sqlExpression;

        // Load destination data using database service - connection will be reused
        var destSqlExpression = $@"
            SELECT DISTINCT [{columnMapping.NewColumn}], [{columnMapping.NewColumn}]
            FROM {columnMapping.NewTableName} 
            ORDER BY [{columnMapping.NewColumn}]";

        using var destReader = await _databaseService.ExecuteReaderAsync(destinationConnectionString, destSqlExpression);
        while (await destReader.ReadAsync())
        {
            analysis.DestinationSampleValues.Add(destReader[0]?.ToString() ?? "", destReader[1]?.ToString() ?? "");
        }
        await destReader.DisposeAsync();

        // Get distinct count - reuses same connection
        var destCountQuery = $"SELECT COUNT(DISTINCT [{columnMapping.NewColumn}]) FROM {columnMapping.NewTableName}";
        var destCountResult = await _databaseService.ExecuteScalarAsync(destinationConnectionString, destCountQuery);
        analysis.DestinationDistinctCount = destCountResult != null ? Convert.ToInt32(destCountResult) : 0;

        // Find mismatched values (in source but not in destination)
        var destValuesSet = analysis.DestinationSampleValues.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        analysis.MismatchedValues = analysis.SourceSampleValues.Values
            .Where(v => !destValuesSet.Contains(v))
            .ToList();

        return analysis;
    }

    /// <summary>
    /// Analyze lookup column with new specification format support
    /// Handles format: [ValueColumnName].[TableName].[ColumnName] = Value [ON [JoinColumnName]]
    /// </summary>
    public async Task<LookupColumnAnalysis> AnalyzeLookupColumnWithSpecAsync(
        DataColumnMapping columnMapping,
        string sourceConnectionString,
        string destinationConnectionString)
    {
        var analysis = new LookupColumnAnalysis();

        // Parse lookup specifications
        var oldLookupSpec = LookupSpecificationParser.Parse(columnMapping.OldLookupTable);
        var newLookupSpec = LookupSpecificationParser.Parse(columnMapping.NewLookupTable);

        // If we have lookup specifications, use them
        if (oldLookupSpec != null || newLookupSpec != null)
        {
            // Load source lookup data
            var sourceLoaded = await LoadLookupDataAsync(
                    sourceConnectionString,
                    columnMapping,
                    oldLookupSpec,
                    analysis.SourceSampleValues,
                    isSource: true);
            if (sourceLoaded.success)
            {
                analysis.SourceDistinctCount = analysis.SourceSampleValues.Count;
                analysis.OldLookupSpec = oldLookupSpec?.ToString();
                analysis.SourceLookupQuery = sourceLoaded.lookupQuery;
            }

            // Load destination lookup data
            var destLoaded = await LoadLookupDataAsync(
                    destinationConnectionString,
                    columnMapping,
                    newLookupSpec,
                    analysis.DestinationSampleValues,
                    isSource: false);
            if (destLoaded.success)
            {
                analysis.DestinationDistinctCount = analysis.DestinationSampleValues.Count;
                analysis.NewLookupSpec = newLookupSpec?.ToString();
                analysis.DestinationLookupQuery = destLoaded.lookupQuery;
            }

            // Compare values
            var destValuesSet = analysis.DestinationSampleValues.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            analysis.MismatchedValues = analysis.SourceSampleValues.Values
                .Where(v => !destValuesSet.Contains(v))
                .ToList();

            // Check if filter values match
            if (oldLookupSpec != null && newLookupSpec != null)
            {
                if (oldLookupSpec.ColumnName == newLookupSpec.ColumnName &&
                    oldLookupSpec.FilterValue != newLookupSpec.FilterValue)
                {
                    analysis.LookupFilterMismatch = true;
                    analysis.LookupFilterMessage =
                        $"Lookup filter mismatch: Old={oldLookupSpec.FilterValue}, New={newLookupSpec.FilterValue}";
                }
            }
        }
        else
        {
            // Fallback to standard lookup analysis (without specification)
            analysis = await AnalyzeLookupColumnAsync(columnMapping, sourceConnectionString, destinationConnectionString);
        }

        // Count mismatches
        if (analysis.MismatchedValues.Any())
        {
            var countQuery = "";
            var sourceTableName = columnMapping.OldTableName;
            var sourceColumnName = columnMapping.OldColumn;
            // Build a query to count records with values not in destination, grouped by value
            var mismatchedValuesList = string.Join(",", analysis.MismatchedValues.Select(v => $"'{v.Replace("'", "''")}'"));

            if (oldLookupSpec != null)
            {
                var joinStr = @$"LEFT JOIN {oldLookupSpec.TableName} ON {oldLookupSpec.TableName}.{oldLookupSpec.JoinColumnName} = {sourceTableName}.{sourceColumnName}";
                if (sourceTableName == oldLookupSpec.TableName)
                    joinStr = "";
                countQuery = $@"
                    SELECT [{sourceColumnName}], [{oldLookupSpec.ValueColumnName}], COUNT(*) as RecordCount
                    FROM {sourceTableName}
                    {joinStr}
                    GROUP BY [{sourceColumnName}], [{oldLookupSpec.ValueColumnName}]
                    ORDER BY RecordCount DESC";
            }
            else
            {
                countQuery = $@"
                    SELECT [{sourceColumnName}], [{sourceColumnName}], COUNT(*) as RecordCount
                    FROM {sourceTableName}
                    GROUP BY [{sourceColumnName}], [{sourceColumnName}]
                    ORDER BY RecordCount DESC";
            }

            // Query the actual count of records in the source for each mismatched value
            // Use database service - connection will be reused
            using var srcReader = await _databaseService.ExecuteReaderAsync(sourceConnectionString, countQuery);
            var valueCounts = new Dictionary<string, string>();
            var totalAffectedRecords = 0;
            while (await srcReader.ReadAsync())
            {
                var value = string.IsNullOrEmpty(srcReader[1]?.ToString()) ? "NULL" : srcReader[1]?.ToString() ?? "";
                var count = srcReader.GetInt32(2);
                valueCounts[value] = srcReader[0]?.ToString() + " :> " + count;
                totalAffectedRecords += count;
            }

            // Store the counts for reporting (you may need to add this property to LookupColumnAnalysis)
            // analysis.MismatchedValueCounts = valueCounts;

            analysis.AffectedRecordCountQuery = countQuery;
            if (valueCounts.ContainsKey("NULL"))
            {
                analysis.MismatchedValues.Add("NULL");
                analysis.SourceSampleValues.Add("NULL", "NULL");
            }

            Console.WriteLine($"Found {analysis.MismatchedValues.Count} mismatched values affecting {totalAffectedRecords} records in {sourceTableName}.{sourceColumnName}");
            foreach (var kvp in valueCounts.OrderByDescending(x => x.Value))
            {
                if (analysis.MismatchedValues.IndexOf(kvp.Key) == -1)
                    continue;
                analysis.MismatchedValues[analysis.MismatchedValues.IndexOf(kvp.Key)] = $"'{kvp.Key}' {kvp.Value} records";
                Console.WriteLine($"  Value '{kvp.Key}': {kvp.Value} records");
            }
        }

        return analysis;
    }

    /// <summary>
    /// Load lookup data based on specification
    /// </summary>
    private async Task<(bool success, string lookupQuery)> LoadLookupDataAsync(
        string connectionString,
        DataColumnMapping columnMapping,
        LookupTableSpec? spec,
        Dictionary<string, string> targetList,
        bool isSource)
    {
        async Task<bool> ExecuteQuery(string sqlQuery)
        {
            using var reader = await _databaseService.ExecuteReaderAsync(connectionString, sqlQuery);
            while (await reader.ReadAsync())
            {
                targetList.Add(reader[0]?.ToString() ?? "", reader[1]?.ToString() ?? "");
            }
            await reader.DisposeAsync();
            return true;
        }

        if (spec == null)
        {
            var sqlExpression = _ruleEngine.GenerateMappingRuleSQL(columnMapping);
            if (string.IsNullOrWhiteSpace(sqlExpression))
                sqlExpression = $@"
                    SELECT DISTINCT [{columnMapping.OldColumn}], [{columnMapping.OldColumn}]
                    FROM {FormatTableName(columnMapping.OldTableName)}
                    ORDER BY [{columnMapping.OldColumn}]";

            return (await ExecuteQuery(sqlExpression), sqlExpression);
        }
        else
        {
            var tableName = FormatTableName(spec.TableName);
            // Query to get values filtered by the specification
            var sqlExpression = $@"
            SELECT DISTINCT TOP 50 [{spec.JoinColumnName}], [{spec.ValueColumnName}]
            FROM {tableName}
            WHERE [{spec.ColumnName}] {spec.FilterOperator} {spec.FilterValue}
            ORDER BY [{spec.ValueColumnName}]";
            var lookupQuery = LookupSpecificationParser.GenerateLookupQuery(spec);
            return (await ExecuteQuery(sqlExpression), lookupQuery);
        }
    }

    private string FormatTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return tableName;

        if (tableName.StartsWith("[") && tableName.Contains("].["))
            return tableName;

        if (!tableName.Contains("."))
            tableName = $"dbo.{tableName}";

        var parts = tableName.Replace("[", "").Replace("]", "").Split('.');
        return string.Join(".", parts.Select(p => $"[{p}]"));
    }
}
