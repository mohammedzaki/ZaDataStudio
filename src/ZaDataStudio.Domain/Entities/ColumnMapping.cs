namespace ZaDataStudio.Domain.Entities;

public class ColumnMapping
{
    public string SourceTable { get; set; } = string.Empty; // Which source table this column comes from
    public string SourceColumn { get; set; } = string.Empty;
    public string DestinationColumn { get; set; } = string.Empty;
    public bool IsKey { get; set; } = false;
}
