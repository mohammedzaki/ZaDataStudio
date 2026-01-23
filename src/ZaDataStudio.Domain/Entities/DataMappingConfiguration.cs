namespace ZaDataStudio.Domain.Entities;

public class DataMappingConfiguration
{
    public List<DataColumnMapping> ColumnMappings { get; set; } = new();
    public Dictionary<string, List<DataColumnMapping>> GroupedByTable { get; set; } = new();
}
