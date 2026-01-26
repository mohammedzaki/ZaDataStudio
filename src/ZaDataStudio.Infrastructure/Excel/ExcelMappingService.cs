using ClosedXML.Excel;
using System.Text;
using ZaDataStudio.Application.Mapping;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Infrastructure.Excel;

public class ExcelMappingService
{
    private readonly MappingRuleEngine _ruleEngine;

    public ExcelMappingService()
    {
        _ruleEngine = new MappingRuleEngine();
    }

    /// <summary>
    /// Parse Excel file containing data mapping configuration
    /// Excel Structure: Single sheet "DataMapping" with 14 columns
    /// </summary>
    public async Task<DataMappingConfiguration> ParseMappingExcelAsync(Stream excelStream)
    {
        var config = new DataMappingConfiguration();

        try
        {
            // Copy stream to memory to avoid synchronous read issues in Blazor
            using var memoryStream = new MemoryStream();
            await excelStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            using var workbook = new XLWorkbook(memoryStream);

            if (!workbook.Worksheets.Contains("DataMapping"))
            {
                throw new Exception("Excel file must contain a 'DataMapping' sheet");
            }

            var sheet = workbook.Worksheet("DataMapping");
            ParseDataMappings(sheet, config);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error parsing Excel mapping file: {ex.Message}", ex);
        }

        return config;
    }

    private string FormatName(string tableName)
    {
        // Ensures table name is properly bracketed for SQL Server
        // e.g., "dbo.Users" -> "[dbo].[Users]"
        // e.g., "[dbo].[Users]" -> "[dbo].[Users]" (unchanged)

        if (string.IsNullOrWhiteSpace(tableName))
            return tableName;

        // Remove any existing brackets
        return tableName.Replace("[", "").Replace("]", "");
    }

    private void ParseDataMappings(IXLWorksheet sheet, DataMappingConfiguration config)
    {
        var rows = sheet.RowsUsed().Skip(1); // Skip header

        foreach (var row in rows)
        {
            var newTableName = row.Cell(1).GetString();
            var newColumn = row.Cell(2).GetString();
            
            // Skip empty rows
            if (string.IsNullOrWhiteSpace(newTableName) && string.IsNullOrWhiteSpace(newColumn))
                continue;

            var mapping = new DataColumnMapping
            {
                NewTableName = FormatName(newTableName),
                NewColumn = FormatName(newColumn),
                NewDataType = row.Cell(3).GetString(),
                NewColumnNullable = ParseNullable(row.Cell(4).GetString()),
                HasLookup = ParseBoolean(row.Cell(5).GetString()),
                NewLookupTable = row.Cell(6).GetString(),
                NewColumnDescription = row.Cell(7).GetString(),
                OldTableName = FormatName(row.Cell(8).GetString()),
                OldColumn = FormatName(row.Cell(9).GetString()),
                OldDataType = row.Cell(10).GetString(),
                OldColumnNullable = ParseNullable(row.Cell(11).GetString()),
                OldLookupTable = row.Cell(12).GetString(),
                MappingRule = row.Cell(13).GetString(),
                Notes = row.Cell(14).GetString(),
                MappingStatus = row.Cell(15).GetString(),
            };

            config.ColumnMappings.Add(mapping);
        }

        // Group by table
        config.GroupedByTable = config.ColumnMappings
            .GroupBy(m => m.NewTableName)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private bool? ParseNullable(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Y", StringComparison.OrdinalIgnoreCase);
    }

    private bool ParseBoolean(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Y", StringComparison.OrdinalIgnoreCase);
    }

    public byte[] GenerateSampleTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("DataMapping");

        // Headers
        sheet.Cell(1, 1).Value = "New Table Name";
        sheet.Cell(1, 2).Value = "New Column";
        sheet.Cell(1, 3).Value = "New DataType";
        sheet.Cell(1, 4).Value = "New Column Nullable";
        sheet.Cell(1, 5).Value = "Has lookup";
        sheet.Cell(1, 6).Value = "New Lookup Table";
        sheet.Cell(1, 7).Value = "New Column Description";
        sheet.Cell(1, 8).Value = "Old System Table Name";
        sheet.Cell(1, 9).Value = "Old Column";
        sheet.Cell(1, 10).Value = "Old DataType";
        sheet.Cell(1, 11).Value = "Old Column Nullable";
        sheet.Cell(1, 12).Value = "Old Lookup Table";
        sheet.Cell(1, 13).Value = "Mapping Rule";
        sheet.Cell(1, 14).Value = "Notes";
        sheet.Cell(1, 15).Value = "Mapping Status";

        // Sample data row 1: Direct mapping
        int row = 2;
        sheet.Cell(row, 1).Value = "dbo.Destination";
        sheet.Cell(row, 2).Value = "DestinationId";
        sheet.Cell(row, 3).Value = "INT";
        sheet.Cell(row, 4).Value = "NO";
        sheet.Cell(row, 5).Value = "NO";
        sheet.Cell(row, 6).Value = "";
        sheet.Cell(row, 7).Value = "Primary key";
        sheet.Cell(row, 8).Value = "OldSystem.Person";
        sheet.Cell(row, 9).Value = "PersonId";
        sheet.Cell(row, 10).Value = "INT";
        sheet.Cell(row, 11).Value = "NO";
        sheet.Cell(row, 12).Value = "";
        sheet.Cell(row, 13).Value = "";
        sheet.Cell(row, 14).Value = "Direct mapping";
        sheet.Cell(row, 15).Value = "Approved";

        // Sample data row 2: Lookup mapping with specification
        row = 3;
        sheet.Cell(row, 1).Value = "dbo.Employee";
        sheet.Cell(row, 2).Value = "EmployeeType";
        sheet.Cell(row, 3).Value = "NVARCHAR(50)";
        sheet.Cell(row, 4).Value = "NO";
        sheet.Cell(row, 5).Value = "YES";
        sheet.Cell(row, 6).Value = "[LookupValues].[LookupTypeId] = 1600";
        sheet.Cell(row, 7).Value = "Employee type from lookup";
        sheet.Cell(row, 8).Value = "OldSystem.Employee";
        sheet.Cell(row, 9).Value = "EmpType";
        sheet.Cell(row, 10).Value = "VARCHAR(50)";
        sheet.Cell(row, 11).Value = "YES";
        sheet.Cell(row, 12).Value = "[OldLookupValues].[LookupTypeId] = 1500";
        sheet.Cell(row, 13).Value = "";
        sheet.Cell(row, 14).Value = "Lookup values filtered by type ID";
        sheet.Cell(row, 15).Value = "Approved";

        // Format
        var headerRange = sheet.Range(1, 1, 1, 15);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        
        // Add notes to lookup columns (using cell notes instead of comments)
        sheet.Cell(2, 6).Value = "Example: [LookupValues].[LookupTypeId] = 1600";
        sheet.Cell(2, 12).Value = "Example: [OldLookupValues].[LookupTypeId] = 1500";
        
        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public string GenerateMigrationSQL(DataMappingConfiguration config)
    {
        // Use the advanced rule engine for SQL generation
        return _ruleEngine.GenerateMigrationSQL(config, includeTransaction: true);
    }

    public string GenerateValidationReport(DataMappingConfiguration config)
    {
        var report = new StringBuilder();
        report.AppendLine("=== Data Mapping Validation Report ===");
        report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();
        
        report.AppendLine($"Total Mappings: {config.ColumnMappings.Count}");
        report.AppendLine($"Total Tables: {config.GroupedByTable.Count}");
        report.AppendLine();
        
        var approved = config.ColumnMappings.Count(m => m.MappingStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase));
        var pending = config.ColumnMappings.Count(m => m.MappingStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase));
        var outofscope = config.ColumnMappings.Count(m => m.MappingStatus.Equals("OutOfScope", StringComparison.OrdinalIgnoreCase));
        var lookupsNeeded = config.ColumnMappings.Count(m => m.HasLookup);
        var lookupsWithSpec = config.ColumnMappings.Count(m => 
            !string.IsNullOrWhiteSpace(m.NewLookupTable) || !string.IsNullOrWhiteSpace(m.OldLookupTable));
        var nullMappings = config.ColumnMappings.Count(m => 
            string.IsNullOrWhiteSpace(m.OldColumn) || 
            m.OldColumn.Equals("N/A", StringComparison.OrdinalIgnoreCase));
        
        report.AppendLine("Status Breakdown:");
        report.AppendLine($"  Approved: {approved}");
        if (pending > 0)
            report.AppendLine($"  Pending: {pending}");
        if (outofscope > 0)
            report.AppendLine($"  Out Of Scope: {outofscope}");
        if (lookupsNeeded > 0)
            report.AppendLine($"  Requires Lookups: {lookupsNeeded}");
        if (lookupsWithSpec > 0)
            report.AppendLine($"  Lookups with Filter Spec: {lookupsWithSpec}");
        if (nullMappings > 0)
            report.AppendLine($"  NULL Mappings (N/A): {nullMappings}");
        report.AppendLine();
        
        // Lookup specification details
        if (lookupsWithSpec > 0)
        {
            report.AppendLine("Lookup Filter Specifications:");
            foreach (var mapping in config.ColumnMappings.Where(m => 
                !string.IsNullOrWhiteSpace(m.NewLookupTable) || !string.IsNullOrWhiteSpace(m.OldLookupTable)))
            {
                report.AppendLine($"  {mapping.NewTableName}.{mapping.NewColumn}:");
                if (!string.IsNullOrWhiteSpace(mapping.OldLookupTable))
                    report.AppendLine($"    Old: {mapping.OldLookupTable}");
                if (!string.IsNullOrWhiteSpace(mapping.NewLookupTable))
                    report.AppendLine($"    New: {mapping.NewLookupTable}");
            }
            report.AppendLine();
        }
        
        return report.ToString();
    }
}


