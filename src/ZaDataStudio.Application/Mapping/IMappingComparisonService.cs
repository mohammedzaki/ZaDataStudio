using System;
using System.Collections.Generic;
using System.Text;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping;

public interface IMappingComparisonService
{
    public Task<MappingComparisonResult> CompareMappingsAsync(
        DataMappingConfiguration dataMappingConfiguration, 
        string sourceConnectionString, 
        string destinationConnectionString,
        IProgress<AnalysisProgress>? progress = null);
}
