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
    //private readonly SemanticLookupMatcher? _semanticMatcher;
    private readonly SemanticMatchingSettingsService? _settingsService;

    public LookupColumnAnalyzer(
        IDatabaseService databaseService, 
        //SemanticLookupMatcher? semanticMatcher = null,
        SemanticMatchingSettingsService? settingsService = null)
    {
        _databaseService = databaseService;
        _ruleEngine = new MappingRuleEngine();
        //_semanticMatcher = semanticMatcher;
        _settingsService = settingsService;
    }

    /// <summary>
    /// Analyze lookup column using mapping rule
    /// </summary>
    public async Task<LookupColumnAnalysis> AnalyzeLookupColumnAsync(
        DataColumnMapping columnMapping,
        string sourceConnectionString,
        string destinationConnectionString,
        Progress<MatchingProgress> progress)
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
            var code = srcReader[0]?.ToString() ?? "";
            var enValue = srcReader[1]?.ToString() ?? "";
            var arValue = srcReader[2]?.ToString() ?? "";
            analysis.SourceSampleValues.Add(code, new LookupValue(code, enValue, arValue));
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
            var code = destReader[0]?.ToString() ?? "";
            var enValue = destReader[1]?.ToString() ?? "";
            var arValue = destReader[2]?.ToString() ?? "";
            analysis.DestinationSampleValues.Add(code, new LookupValue(code, enValue, arValue));
        }
        await destReader.DisposeAsync();

        // Get distinct count - reuses same connection
        var destCountQuery = $"SELECT COUNT(DISTINCT [{columnMapping.NewColumn}]) FROM {columnMapping.NewTableName}";
        var destCountResult = await _databaseService.ExecuteScalarAsync(destinationConnectionString, destCountQuery);
        analysis.DestinationDistinctCount = destCountResult != null ? Convert.ToInt32(destCountResult) : 0;

        // Find mismatched values (in source but not in destination)
        var destValuesSet = analysis.DestinationSampleValues.Values.Select(v => v.EnValue).ToHashSet(StringComparer.OrdinalIgnoreCase);
        analysis.MismatchedValues = analysis.SourceSampleValues.Values.Select(v => v.EnValue)
            .Where(v => !destValuesSet.Contains(v))
            .ToList();

        // Build values mapping for matched and unmatched values
        await BuildValuesMappingAsync(analysis, null, progress);

        return analysis;
    }

    /// <summary>
    /// Analyze lookup column with new specification format support
    /// Handles format: [ValueColumnName].[TableName].[ColumnName] = Value [ON [JoinColumnName]]
    /// </summary>
    public async Task<LookupColumnAnalysis> AnalyzeLookupColumnWithSpecAsync(
        DataColumnMapping columnMapping,
        string sourceConnectionString,
        string destinationConnectionString,
        Progress<MatchingProgress> progress)
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
            var destValuesSet = analysis.DestinationSampleValues.Values.Select(v => v.EnValue).ToHashSet(StringComparer.OrdinalIgnoreCase);
            analysis.MismatchedValues = analysis.SourceSampleValues.Values.Select(v => v.EnValue)
                .Where(v => !destValuesSet.Contains(v))
                .ToList();
        }
        else
        {
            // Fallback to standard lookup analysis (without specification)
            analysis = await AnalyzeLookupColumnAsync(columnMapping, sourceConnectionString, destinationConnectionString, progress);
        }

        // Count mismatches
        var valueCounts = new Dictionary<string, string>();
        if (analysis.MismatchedValues.Any())
        {
            var countQuery = "";
            var sourceTableName = columnMapping.OldTableName;
            var sourceColumnName = columnMapping.OldColumn;

            // Build a query to count records with values not in destination, grouped by value
            if (oldLookupSpec != null)
            {
                countQuery = LookupSpecificationParser.GenerateLookupSqlCountQuery(oldLookupSpec, sourceTableName, sourceColumnName);
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
                analysis.SourceSampleValues.Add("NULL", new LookupValue("NULL", "NULL", "NULL"));
            }

            Console.WriteLine($"Found {analysis.MismatchedValues.Count} mismatched values affecting {totalAffectedRecords} records in {sourceTableName}.{sourceColumnName}");
            var currentMismatchedValues = new List<string>();
            foreach (var kvp in valueCounts.OrderByDescending(x => x.Value))
            {
                if (analysis.MismatchedValues.IndexOf(kvp.Key) == -1)
                    continue;
                currentMismatchedValues.Add($"'{kvp.Key}' {kvp.Value} records");
                Console.WriteLine($"  Value '{kvp.Key}': {kvp.Value} records");
            }
            analysis.MismatchedValues = currentMismatchedValues;
        }

        // Build values mapping for matched and unmatched values
        await BuildValuesMappingAsync(analysis, valueCounts, progress);

        return analysis;
    }

    /// <summary>
    /// Load lookup data based on specification
    /// </summary>
    private async Task<(bool success, string lookupQuery)> LoadLookupDataAsync(
        string connectionString,
        DataColumnMapping columnMapping,
        LookupTableSpec? spec,
        Dictionary<string, LookupValue> targetList,
        bool isSource)
    {
        async Task<bool> ExecuteQuery(string sqlQuery)
        {
            using var reader = await _databaseService.ExecuteReaderAsync(connectionString, sqlQuery);
            while (await reader.ReadAsync())
            {
                var code = reader[0]?.ToString() ?? "";
                var enValue = reader[1]?.ToString() ?? "";
                var arValue = "";
                if (reader.FieldCount >= 3)
                    arValue = reader[2]?.ToString() ?? "";
                targetList.Add(code, new LookupValue(code, enValue, arValue));
            }
            await reader.DisposeAsync();
            return true;
        }

        if (spec == null)
        {
            var sqlExpression = _ruleEngine.GenerateMappingRuleSQL(columnMapping);
            if (string.IsNullOrWhiteSpace(sqlExpression))
                sqlExpression = $@"
                    SELECT DISTINCT [{columnMapping.OldColumn}] AS LookupCode, [{columnMapping.OldColumn}] AS LookupEnValue
                    FROM {FormatTableName(columnMapping.OldTableName)}
                    ORDER BY [{columnMapping.OldColumn}]";

            return (await ExecuteQuery(sqlExpression), sqlExpression);
        }
        else 
        {
            var tableName = FormatTableName(spec.TableName);
            // Query to get values filtered by the specification
            var sqlExpression = LookupSpecificationParser.GenerateLookupSqlExpression(spec);
            var lookupQuery = LookupSpecificationParser.GenerateLookupQuery(spec);
            return (await ExecuteQuery(sqlExpression), lookupQuery);
        }
    }

    /// <summary> 
    /// Build values mapping showing matched and unmatched values
    /// Uses semantic matching for unmatched values if available
    /// </summary>
    private async Task BuildValuesMappingAsync(LookupColumnAnalysis analysis, Dictionary<string, string>? valueCounts, Progress<MatchingProgress> progress)
    {
        analysis.ValuesMapping.Clear();

        // Create a dictionary for quick destination lookup by value (case-insensitive)
        var destByValue = new Dictionary<string, LookupValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var dest in analysis.DestinationSampleValues)
        {
            if (!destByValue.ContainsKey(dest.Value.EnValue))
            {
                destByValue[dest.Value.EnValue] = new LookupValue(dest.Key, dest.Value.EnValue, dest.Value.ArValue);
            }
        }

        // Collect unmatched source values for batch semantic matching
        var unmatchedSources = new List<(string Key, LookupValue Value)>();

        // Map all source values
        foreach (var source in analysis.SourceSampleValues)
        {
            var mapping = new LookupValueMapping
            {
                SourceLookupCode = source.Key,
                SourceLookupEnValue = source.Value.EnValue,
                SourceLookupArValue = source.Value.ArValue
            };

            // Try to find matching destination value (case-insensitive exact match)
            if (destByValue.TryGetSimilarValue(source.Value, out var destMatch))
            {
                mapping.DestinationLookupCode = destMatch.Code;
                mapping.DestinationLookupEnValue = destMatch.EnValue;
                mapping.DestinationLookupArValue = destMatch.ArValue;
            }
            else
            {
                // No exact match - collect for semantic matching
                unmatchedSources.Add((source.Key, source.Value));

                // Temporarily set as unmatched
                mapping.DestinationLookupCode = string.Empty;
                mapping.DestinationLookupEnValue = string.Empty;
                mapping.DestinationLookupArValue = string.Empty;
            }
            if (valueCounts != null && valueCounts.ContainsKey(mapping.SourceLookupEnValue))
            {
                var count = valueCounts[mapping.SourceLookupEnValue].IndexOf('>') + 2;
                mapping.SourceLookupRecordsCount = valueCounts[mapping.SourceLookupEnValue].Remove(0, count);
                analysis.ValuesMapping.Add(mapping);
            } 
            //else analysis.ValuesMapping.Add(mapping);
        }

        // Try semantic matching for unmatched values
        // Use settings service to create matcher with current settings (runtime switching)
        // or fallback to injected _semanticMatcher
        var matcher = _settingsService?.CreateMatcher();

        if (matcher != null && unmatchedSources.Any() && analysis.DestinationSampleValues.Any())
        {
            try
            {
                var destValues = analysis.DestinationSampleValues.Values.Select(v => v.EnValue).ToList();
                var sourceValues = unmatchedSources.Select(s => s.Value.EnValue).ToList();

                // Batch match for better performance
                var semanticMatches = await matcher.BatchMatchAsync(
                    sourceValues, 
                    destValues, 
                    progress,
                    cancellationToken: default);

                // Update mappings with semantic matches
                foreach (var unmatched in unmatchedSources)
                {
                    if (semanticMatches.TryGetValue(unmatched.Value.EnValue, out var match) && match.Match != null)
                    {
                        // Find the mapping and update it
                        var mapping = analysis.ValuesMapping.FirstOrDefault(m => 
                            m.SourceLookupCode == unmatched.Key && 
                            string.IsNullOrEmpty(m.DestinationLookupCode));

                        if (mapping != null && destByValue.TryGetValue(match.Match, out var destMatch))
                        {
                            mapping.DestinationLookupCode = destMatch.Code;
                            mapping.DestinationLookupEnValue = destMatch.EnValue;
                            mapping.DestinationLookupArValue = destMatch.ArValue;
                            mapping.SemanticSimilarity = match.Similarity;

                            Console.WriteLine($"Semantic match: '{unmatched.Value.EnValue}' → '{match.Match}' (similarity: {match.Similarity:P0})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Semantic matching failed: {ex.Message}");
                // Continue without semantic matching - exact matches are already done
            }
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
