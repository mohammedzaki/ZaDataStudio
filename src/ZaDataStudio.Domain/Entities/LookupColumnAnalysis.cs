using System;
using System.Collections.Generic;
using System.Text;

namespace ZaDataStudio.Domain.Entities
{
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
        public Dictionary<string, string> SourceSampleValues { get; set; } = new();
        public Dictionary<string, string> DestinationSampleValues { get; set; } = new();
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
        public string SourceCode { get; set; } = string.Empty;
        public string SourceValue { get; set; } = string.Empty;
        public string DestinationCode { get; set; } = string.Empty;
        public string DestinationValue { get; set; } = string.Empty;
    }
}

