using System;
using System.Collections.Generic;
using System.Text;

namespace ZaDataStudio.Domain.Entities
{
    public class DatatypeComparison
    {
        public string SourceTable { get; set; } = string.Empty;
        public string SourceColumn { get; set; } = string.Empty;
        public string DestinationTable { get; set; } = string.Empty;
        public string DestinationColumn { get; set; } = string.Empty;
        public string SourceDataType { get; set; } = string.Empty;
        public string DestinationDataType { get; set; } = string.Empty;
        public List<string> Issues { get; set; } = new();
        public bool HasIssues => Issues.Any();
    }
}
