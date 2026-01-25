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
        public int SourceDistinctCount { get; set; }
        public int DestinationDistinctCount { get; set; }
        public List<string> SourceSampleValues { get; set; } = new();
        public List<string> DestinationSampleValues { get; set; } = new();
        public List<string> MismatchedValues { get; set; } = new();
    }
}
