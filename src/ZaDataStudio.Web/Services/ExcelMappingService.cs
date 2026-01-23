using ClosedXML.Excel;
using System.Text;

namespace ZaDataStudio.Web.Services;

public class ExcelMappingService
{
    private readonly MappingRuleEngine _ruleEngine;
    private readonly MappingValidator _validator;

    public ExcelMappingService()
    {
        _ruleEngine = new MappingRuleEngine();
        _validator = new MappingValidator();
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
                NewTableName = newTableName,
                NewColumn = newColumn,
                NewDataType = row.Cell(3).GetString(),
                NewColumnNullable = ParseNullable(row.Cell(4).GetString()),
                HasLookup = ParseBoolean(row.Cell(5).GetString()),
                NewColumnDescription = row.Cell(6).GetString(),
                OldTableName = row.Cell(7).GetString(),
                OldColumn = row.Cell(8).GetString(),
                OldDataType = row.Cell(9).GetString(),
                OldColumnNullable = ParseNullable(row.Cell(10).GetString()),
                MappingRule = row.Cell(11).GetString(),
                Notes = row.Cell(12).GetString(),
                MappingStatus = row.Cell(13).GetString(),
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
        sheet.Cell(1, 6).Value = "New Column Description";
        sheet.Cell(1, 7).Value = "Old System Table Name";
        sheet.Cell(1, 8).Value = "Old Column";
        sheet.Cell(1, 9).Value = "Old DataType";
        sheet.Cell(1, 10).Value = "Old Column Nullable";
        sheet.Cell(1, 11).Value = "Mapping Status";
        sheet.Cell(1, 12).Value = "Notes";

        // Sample data
        int row = 2;
        sheet.Cell(row, 1).Value = "dbo.Destination";
        sheet.Cell(row, 2).Value = "DestinationId";
        sheet.Cell(row, 3).Value = "INT";
        sheet.Cell(row, 4).Value = "NO";
        sheet.Cell(row, 5).Value = "NO";
        sheet.Cell(row, 6).Value = "Primary key";
        sheet.Cell(row, 7).Value = "OldSystem.Person";
        sheet.Cell(row, 8).Value = "PersonId";
        sheet.Cell(row, 9).Value = "INT";
        sheet.Cell(row, 10).Value = "NO";
        sheet.Cell(row, 11).Value = "Approved";
        sheet.Cell(row, 12).Value = "Direct mapping";

        // Format
        var headerRange = sheet.Range(1, 1, 1, 12);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
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
        // Use the validator for comprehensive reporting
        var validationReport = _validator.Validate(config);
        
        var report = new StringBuilder();
        report.AppendLine("=== Data Mapping Validation Report ===");
        report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();
        
        report.AppendLine($"Total Mappings: {config.ColumnMappings.Count}");
        report.AppendLine($"Total Tables: {config.GroupedByTable.Count}");
        report.AppendLine();
        
        var approved = config.ColumnMappings.Count(m => m.MappingStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase));
        var pending = config.ColumnMappings.Count(m => m.MappingStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase));
        var lookupsNeeded = config.ColumnMappings.Count(m => m.HasLookup);
        var nullMappings = config.ColumnMappings.Count(m => 
            string.IsNullOrWhiteSpace(m.OldColumn) || 
            m.OldColumn.Equals("N/A", StringComparison.OrdinalIgnoreCase));
        
        report.AppendLine("Status Breakdown:");
        report.AppendLine($"  Approved: {approved}");
        if (pending > 0)
            report.AppendLine($"  Pending: {pending}");
        if (lookupsNeeded > 0)
            report.AppendLine($"  Requires Lookups: {lookupsNeeded}");
        if (nullMappings > 0)
            report.AppendLine($"  NULL Mappings (N/A): {nullMappings}");
        report.AppendLine();
        
        // Add validation results
        if (validationReport.IsValid)
        {
            report.AppendLine("✓ Validation: PASSED - No errors found");
        }
        else
        {
            report.AppendLine($"✗ Validation: FAILED - {validationReport.Errors.Count} error(s)");
            report.AppendLine();
            report.AppendLine("Errors:");
            foreach (var error in validationReport.Errors)
            {
                report.AppendLine($"  - {error}");
            }
        }
        
        if (validationReport.Warnings.Any())
        {
            report.AppendLine();
            report.AppendLine($"Warnings ({validationReport.Warnings.Count}):");
            foreach (var warning in validationReport.Warnings)
            {
                report.AppendLine($"  - {warning}");
            }
        }
        
        return report.ToString();
    }
}

public class DataMappingConfiguration
{
    public List<DataColumnMapping> ColumnMappings { get; set; } = new();
    public Dictionary<string, List<DataColumnMapping>> GroupedByTable { get; set; } = new();
}

public class DataColumnMapping
{
    public string NewTableName { get; set; } = string.Empty;
    public string NewColumn { get; set; } = string.Empty;
    public string NewDataType { get; set; } = string.Empty;
    public bool? NewColumnNullable { get; set; }
    public bool HasLookup { get; set; }
    public string NewColumnDescription { get; set; } = string.Empty;
    public string OldTableName { get; set; } = string.Empty;
    public string OldColumn { get; set; } = string.Empty;
    public string OldDataType { get; set; } = string.Empty;
    public bool? OldColumnNullable { get; set; }
    public string MappingRule { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string MappingStatus { get; set; } = string.Empty;
}
