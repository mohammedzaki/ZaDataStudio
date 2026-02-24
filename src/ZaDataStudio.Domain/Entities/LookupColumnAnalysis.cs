using System;
using System.Collections.Generic;
using System.Text;

namespace ZaDataStudio.Domain.Entities;

public class LookupColumnAnalysis
{
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public string SourceColumn { get; set; } = string.Empty;
    public string SourceLookupQuery { get; set; } = string.Empty;
    public int SourceDistinctCount { get; set; }
    public string DestinationLookupQuery { get; set; } = string.Empty;
    public int DestinationDistinctCount { get; set; }
    public Dictionary<string, LookupValue> SourceSampleValues { get; set; } = new();
    public Dictionary<string, LookupValue> DestinationSampleValues { get; set; } = new();
    public List<string> MismatchedValues { get; set; } = new();
    public List<LookupValueMapping> ValuesMapping { get; set; } = new();
    public string AffectedRecordCountQuery { get; set; } = string.Empty;

    // New properties for lookup specification support
    public string? OldLookupSpec { get; set; }
    public string? NewLookupSpec { get; set; }
    public bool LookupFilterMismatch { get; set; }
    public string? LookupFilterMessage { get; set; }

    public bool? HasError { get; set; }
    public string? ErrorMessage { get; set; }
}

public class LookupValueMapping
{
    public string SourceLookupCode { get; set; } = string.Empty;
    public string SourceLookupEnValue { get; set; } = string.Empty;
    public string SourceLookupArValue { get; set; } = string.Empty;
    public string DestinationLookupCode { get; set; } = string.Empty;
    public string DestinationLookupEnValue { get; set; } = string.Empty;
    public string DestinationLookupArValue { get; set; } = string.Empty;

    /// <summary>
    /// Semantic similarity score (0-1) if matched using AI
    /// Null if matched exactly or not using semantic matching
    /// </summary>
    public double? SemanticSimilarity { get; set; }

    /// <summary>
    /// Convenience property to get destination lookup value
    /// </summary>
    public string DestinationLookupValue => DestinationLookupEnValue;
}

public record LookupValue(string Code, string EnValue, string ArValue);