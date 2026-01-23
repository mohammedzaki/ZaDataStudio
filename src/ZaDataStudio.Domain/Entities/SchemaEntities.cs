namespace ZaDataStudio.Domain.Entities;

public class TableSchema
{
    public string Schema { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<ColumnSchema> Columns { get; set; } = new();
}

public class ColumnSchema
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public byte? NumericPrecision { get; set; }
    public int? NumericScale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }

    public string DisplayType
    {
        get
        {
            var type = DataType;
            if (MaxLength.HasValue && MaxLength.Value > 0)
                type += $"({MaxLength})";
            else if (NumericPrecision.HasValue)
            {
                type += $"({NumericPrecision}";
                if (NumericScale.HasValue)
                    type += $",{NumericScale}";
                type += ")";
            }
            return type;
        }
    }
}

public class ColumnTypeInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public byte? NumericPrecision { get; set; }
    public int? NumericScale { get; set; }
    public bool IsNullable { get; set; }

    public string DisplayType
    {
        get
        {
            var type = DataType;
            if (MaxLength.HasValue && MaxLength.Value > 0)
                type += $"({MaxLength})";
            else if (NumericPrecision.HasValue)
            {
                type += $"({NumericPrecision}";
                if (NumericScale.HasValue)
                    type += $",{NumericScale}";
                type += ")";
            }
            return type + (IsNullable ? " NULL" : " NOT NULL");
        }
    }
}

public class ComparisonResult
{
    public List<string> TablesOnlyInSource { get; set; } = new();
    public List<string> TablesOnlyInDestination { get; set; } = new();
    public List<TableDifference> TableDifferences { get; set; } = new();

    public bool HasDifferences =>
        TablesOnlyInSource.Any() ||
        TablesOnlyInDestination.Any() ||
        TableDifferences.Any(d => d.HasDifferences);
}

public class TableDifference
{
    public string TableName { get; set; } = string.Empty;
    public List<string> ColumnsOnlyInSource { get; set; } = new();
    public List<string> ColumnsOnlyInDestination { get; set; } = new();
    public Dictionary<string, List<string>> ColumnDifferences { get; set; } = new();

    public bool HasDifferences =>
        ColumnsOnlyInSource.Any() ||
        ColumnsOnlyInDestination.Any() ||
        ColumnDifferences.Any();
}

public class ColumnTypeComparisonResult
{
    public List<ColumnTypeDifference> Differences { get; set; } = new();
    
    public bool HasDifferences => Differences.Any();
}

public class ColumnTypeDifference
{
    public string SourceColumn { get; set; } = string.Empty;
    public string DestinationColumn { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string DestinationType { get; set; } = string.Empty;
    public List<string> DifferenceDetails { get; set; } = new();
}
