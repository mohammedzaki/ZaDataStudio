namespace ZaDataStudio.Domain.Entities;

public class DataColumnMapping
{
    public string NewTableName { get; set; } = string.Empty;
    public string NewColumn { get; set; } = string.Empty;
    public string NewDataType { get; set; } = string.Empty;
    public bool? NewColumnNullable { get; set; }
    public bool HasLookup { get; set; }
    public string NewLookupTable { get; set; } = string.Empty;
    public string NewColumnDescription { get; set; } = string.Empty;
    public string OldTableName { get; set; } = string.Empty;
    public string OldColumn { get; set; } = string.Empty;
    public string OldDataType { get; set; } = string.Empty;
    public bool? OldColumnNullable { get; set; }
    public string OldLookupTable { get; set; } = string.Empty;
    public string MappingRule { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string MappingStatus { get; set; } = string.Empty;
}
