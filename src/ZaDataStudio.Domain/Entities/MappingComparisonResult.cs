using System;
using System.Collections.Generic;
using System.Text;

namespace ZaDataStudio.Domain.Entities
{
    public class MappingComparisonResult
    {
        public List<LookupColumnAnalysis> LookupAnalysis { get; set; } = new();
        public List<DatatypeComparison> DatatypeComparisons { get; set; } = new();
    }
}
