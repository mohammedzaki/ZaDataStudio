namespace ZaDataStudio.Domain.Entities;

public class TableMapping
{
    public string SourceTable { get; set; } = string.Empty;
    public List<string> SourceTables { get; set; } = new(); // Multiple source tables
    public string DestinationTable { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = true;
    public List<ColumnMapping> ColumnMappings { get; set; } = new();
    public bool CompareData { get; set; } = false;
    public List<string> AvailableSourceColumns { get; set; } = new();
    public List<string> AvailableDestinationColumns { get; set; } = new();
    
    // Helper to get all source tables (single or multiple)
    public List<string> GetAllSourceTables() 
    {
        if (SourceTables.Any())
            return SourceTables;
        if (!string.IsNullOrEmpty(SourceTable))
            return new List<string> { SourceTable };
        return new List<string>();
    }
}
