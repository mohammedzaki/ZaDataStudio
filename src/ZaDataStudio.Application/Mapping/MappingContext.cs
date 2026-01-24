using System;
using System.Collections.Generic;
using System.Text;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// Context object passed to mapping rules (Strategy pattern)
/// </summary>
public class MappingContext
{
    public string DestinationTable { get; set; } = string.Empty;
    public List<string> SourceTables { get; set; } = new();
    public List<DataColumnMapping> AllMappings { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
