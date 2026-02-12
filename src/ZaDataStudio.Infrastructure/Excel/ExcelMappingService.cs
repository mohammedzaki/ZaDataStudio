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
                InsertOrder = ParseInsertOrder(row.Cell(8).GetString()),
                OldTableName = FormatName(row.Cell(9).GetString()),
                OldColumn = FormatName(row.Cell(10).GetString()),
                OldDataType = row.Cell(11).GetString(),
                OldColumnNullable = ParseNullable(row.Cell(12).GetString()),
                OldLookupTable = row.Cell(13).GetString(),
                MappingRule = row.Cell(14).GetString(),
                Notes = row.Cell(15).GetString(),
                MappingStatus = row.Cell(16).GetString(),
            };

            config.ColumnMappings.Add(mapping);
        }

        // Group by table
        config.GroupedByTable = config.ColumnMappings
            .GroupBy(m => m.NewTableName)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private int? ParseInsertOrder(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value, out var order) ? order : null;
    }

    private bool? ParseNullable(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("NULL", StringComparison.OrdinalIgnoreCase);
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
        sheet.Cell(1, 8).Value = "Insert Order";
        sheet.Cell(1, 9).Value = "Old System Table Name";
        sheet.Cell(1, 10).Value = "Old Column";
        sheet.Cell(1, 11).Value = "Old DataType";
        sheet.Cell(1, 12).Value = "Old Column Nullable";
        sheet.Cell(1, 13).Value = "Old Lookup Table";
        sheet.Cell(1, 14).Value = "Mapping Rule";
        sheet.Cell(1, 15).Value = "Notes";
        sheet.Cell(1, 16).Value = "Mapping Status";

        // Sample data row 1: Direct mapping
        int row = 2;
        sheet.Cell(row, 1).Value = "dbo.Destination";
        sheet.Cell(row, 2).Value = "DestinationId";
        sheet.Cell(row, 3).Value = "INT";
        sheet.Cell(row, 4).Value = "NO";
        sheet.Cell(row, 5).Value = "NO";
        sheet.Cell(row, 6).Value = "";
        sheet.Cell(row, 7).Value = "Primary key";
        sheet.Cell(row, 8).Value = 1;
        sheet.Cell(row, 9).Value = "OldSystem.Person";
        sheet.Cell(row, 10).Value = "PersonId";
        sheet.Cell(row, 11).Value = "INT";
        sheet.Cell(row, 12).Value = "NO";
        sheet.Cell(row, 13).Value = "";
        sheet.Cell(row, 14).Value = "";
        sheet.Cell(row, 15).Value = "Direct mapping";
        sheet.Cell(row, 16).Value = "Approved";

        // Sample data row 2: Lookup mapping with specification
        row = 3;
        sheet.Cell(row, 1).Value = "dbo.Employee";
        sheet.Cell(row, 2).Value = "EmployeeType";
        sheet.Cell(row, 3).Value = "NVARCHAR(50)";
        sheet.Cell(row, 4).Value = "NO";
        sheet.Cell(row, 5).Value = "YES";
        sheet.Cell(row, 6).Value = "[Name].[LookupValues].[LookupTypeId] = 1600";
        sheet.Cell(row, 7).Value = "Employee type from lookup";
        sheet.Cell(row, 8).Value = 2;
        sheet.Cell(row, 9).Value = "OldSystem.Employee";
        sheet.Cell(row, 10).Value = "EmpType";
        sheet.Cell(row, 11).Value = "VARCHAR(50)";
        sheet.Cell(row, 12).Value = "YES";
        sheet.Cell(row, 13).Value = "[Name].[OldLookupValues].[LookupTypeId] = 1500";
        sheet.Cell(row, 14).Value = "";
        sheet.Cell(row, 15).Value = "Lookup values filtered by type ID";
        sheet.Cell(row, 16).Value = "Approved";

        // Format
        var headerRange = sheet.Range(1, 1, 1, 16);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

        // Add notes to lookup columns (using cell notes instead of comments)
        sheet.Cell(2, 6).Value = "Example: [Name].[LookupValues].[LookupTypeId] = 1600";
        sheet.Cell(2, 13).Value = "Example: [Name].[OldLookupValues].[LookupTypeId] = 1500";

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public string GenerateMigrationSQL(
        DataMappingConfiguration config,
        MappingComparisonResult analysisResult,
        List<DatatypeComparison> datatypeComparisons,
        string sourceDatabase = "",
        string destinationDatabase = "")
    {
        // Use the advanced rule engine for SQL generation
        return _ruleEngine.GenerateMigrationSQL(
            config, 
            analysisResult, 
            datatypeComparisons, 
            sourceDatabase,
            destinationDatabase,
            includeTransaction: true);
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

    /// <summary>
    /// Generate Excel file with analysis results
    /// Includes main mapping sheet with AnalysisResult column + separate tabs for each lookup analysis
    /// </summary>
    public byte[] GenerateAnalysisExcel(
        DataMappingConfiguration config, 
        MappingComparisonResult analysisResult,
        List<DatatypeComparison> datatypeComparisons)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        if (analysisResult == null)
            throw new ArgumentNullException(nameof(analysisResult));

        using var workbook = new XLWorkbook();

        // Track lookup sheet names for hyperlinking
        var lookupSheetNames = new Dictionary<string, string>(); // Key: TableName.ColumnName, Value: SheetName

        // 1. Create main DataMapping sheet with analysis results (pass lookup sheet names for later linking)
        var mainSheet = workbook.Worksheets.Add("DataMapping");

        // 2. Create separate tabs for each lookup analysis (only if there are lookups)
        if (analysisResult.LookupAnalysis != null && analysisResult.LookupAnalysis.Any())
        {
            int lookupIndex = 1;
            foreach (var lookupAnalysis in analysisResult.LookupAnalysis)
            {
                try
                {
                    var sheetName = SanitizeSheetName($"{lookupAnalysis.ColumnName}_{lookupAnalysis.SourceTable}");

                    // Ensure unique sheet name
                    if (workbook.Worksheets.Any(ws => ws.Name == sheetName))
                    {
                        sheetName = SanitizeSheetName($"{sheetName}_{lookupIndex}");
                    }

                    var lookupSheet = workbook.Worksheets.Add(sheetName);
                    GenerateLookupAnalysisSheet(lookupSheet, lookupAnalysis);

                    // Store sheet name for hyperlinking
                    var key = $"{lookupAnalysis.TableName}.{lookupAnalysis.ColumnName}";
                    lookupSheetNames[key] = sheetName;

                    lookupIndex++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating lookup sheet for {lookupAnalysis.ColumnName}: {ex.Message}");
                }
            }
        }

        // Generate main sheet with hyperlinks to lookup sheets
        GenerateMainMappingSheet(mainSheet, config, analysisResult, datatypeComparisons, lookupSheetNames);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void GenerateMainMappingSheet(
        IXLWorksheet sheet, 
        DataMappingConfiguration config,
        MappingComparisonResult analysisResult,
        List<DatatypeComparison> datatypeComparisons,
        Dictionary<string, string> lookupSheetNames)
    {
        if (sheet == null || config == null)
            return;

        // Headers (same as template + AnalysisResult column)
        sheet.Cell(1, 1).Value = "New Table Name";
        sheet.Cell(1, 2).Value = "New Column";
        sheet.Cell(1, 3).Value = "New DataType";
        sheet.Cell(1, 4).Value = "New Column Nullable";
        sheet.Cell(1, 5).Value = "Has lookup";
        sheet.Cell(1, 6).Value = "New Lookup Table";
        sheet.Cell(1, 7).Value = "New Column Description";
        sheet.Cell(1, 8).Value = "Insert Order";
        sheet.Cell(1, 9).Value = "Old System Table Name";
        sheet.Cell(1, 10).Value = "Old Column";
        sheet.Cell(1, 11).Value = "Old DataType";
        sheet.Cell(1, 12).Value = "Old Column Nullable";
        sheet.Cell(1, 13).Value = "Old Lookup Table";
        sheet.Cell(1, 14).Value = "Mapping Rule";
        sheet.Cell(1, 15).Value = "Notes";
        sheet.Cell(1, 16).Value = "Mapping Status";
        sheet.Cell(1, 17).Value = "AnalysisResult";  // New column

        // Format header
        var headerRange = sheet.Range(1, 1, 1, 17);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

        // Populate data rows
        int row = 2;
        int maxRow = 1048576; // Excel's maximum row limit

        var prevTable = config.ColumnMappings.First().NewTableName ?? "";

        foreach (var mapping in config.ColumnMappings)
        {
            if (row > maxRow)
            {
                Console.WriteLine($"Warning: Exceeded Excel row limit at row {row}. Stopping data export.");
                break;
            }

            try
            {
                if (mapping.NewTableName != prevTable)
                {
                    // Add gray separator
                    AddGraySeparator(sheet, row, 17);
                    row++;
                }
                sheet.Cell(row, 1).Value = mapping.NewTableName ?? "";
                sheet.Cell(row, 2).Value = mapping.NewColumn ?? "";
                sheet.Cell(row, 3).Value = mapping.NewDataType ?? "";
                sheet.Cell(row, 4).Value = mapping.NewColumnNullable.HasValue
                    ? (mapping.NewColumnNullable.Value ? "YES" : "NO")
                    : "";
                sheet.Cell(row, 5).Value = mapping.HasLookup ? "YES" : "NO";
                sheet.Cell(row, 6).Value = mapping.NewLookupTable ?? "";
                sheet.Cell(row, 7).Value = mapping.NewColumnDescription ?? "";
                sheet.Cell(row, 8).Value = mapping.InsertOrder.HasValue ? mapping.InsertOrder.Value : "";
                sheet.Cell(row, 9).Value = mapping.OldTableName ?? "";
                sheet.Cell(row, 10).Value = mapping.OldColumn ?? "";
                sheet.Cell(row, 11).Value = mapping.OldDataType ?? "";
                sheet.Cell(row, 12).Value = mapping.OldColumnNullable.HasValue
                    ? (mapping.OldColumnNullable.Value ? "YES" : "NO")
                    : "";
                sheet.Cell(row, 13).Value = mapping.OldLookupTable ?? "";
                sheet.Cell(row, 14).Value = mapping.MappingRule ?? "";
                sheet.Cell(row, 15).Value = mapping.Notes ?? "";
                sheet.Cell(row, 16).Value = mapping.MappingStatus ?? "";

                // Generate analysis result
                var analysisText = GenerateAnalysisResult(mapping, analysisResult, datatypeComparisons);
                sheet.Cell(row, 17).Value = analysisText ?? "✓ OK";

                // Color code the analysis result
                if (analysisText.Contains("✓ OK") || analysisText.Contains("✓ Lookup") || analysisText.Contains("✓ Type"))
                {
                    sheet.Cell(row, 17).Style.Fill.BackgroundColor = XLColor.LightGreen;
                }
                else if (analysisText.Contains("⚠"))
                {
                    sheet.Cell(row, 17).Style.Fill.BackgroundColor = XLColor.Yellow;
                }
                else if (analysisText.Contains("✗"))
                {
                    sheet.Cell(row, 17).Style.Fill.BackgroundColor = XLColor.LightPink;
                }

                // Add hyperlink to lookup analysis sheet if it exists
                if (lookupSheetNames != null)
                {
                    var key = $"{mapping.NewTableName}.{mapping.NewColumn}";
                    if (lookupSheetNames.ContainsKey(key))
                    {
                        var lookupSheetName = lookupSheetNames[key];
                        var cell = sheet.Cell(row, 17);

                        // Add hyperlink to the cell
                        try
                        {
                            cell.SetHyperlink(new XLHyperlink($"'{lookupSheetName}'!A1"));
                            cell.Style.Font.FontColor = XLColor.Blue;
                            cell.Style.Font.Underline = XLFontUnderlineValues.Single;

                            // Append link indicator to text
                            cell.Value = $"{analysisText} 🔗";
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error adding hyperlink for {key}: {ex.Message}");
                        }
                    }
                }

                row++; // Blank row
                prevTable = mapping.NewTableName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing row {row} for mapping {mapping.NewTableName}.{mapping.NewColumn}: {ex.Message}");
            }
        }

        // Auto-fit columns
        try
        {
            sheet.Columns().AdjustToContents();
            sheet.SheetView.FreezeRows(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adjusting columns: {ex.Message}");
        }
    }

    private string GenerateAnalysisResult(
        DataColumnMapping mapping,
        MappingComparisonResult analysisResult,
        List<DatatypeComparison> datatypeComparisons)
    {
        var results = new List<string>();

        // Check lookup analysis first (priority)
        var lookupAnalysis = analysisResult.LookupAnalysis.FirstOrDefault(l => 
            l.TableName == mapping.NewTableName && l.ColumnName == mapping.NewColumn);

        if (lookupAnalysis != null)
        {
            if (lookupAnalysis.MismatchedValues.Any())
            {
                results.Add($"⚠ {lookupAnalysis.MismatchedValues.Count} mismatched value(s)");
            }
            else if (lookupAnalysis.SourceSampleValues.Any() || lookupAnalysis.DestinationSampleValues.Any())
            {
                results.Add("✓ Lookup values match");
            }
            else
            {
                results.Add("⚠ No lookup values found");
            }

            if (lookupAnalysis.LookupFilterMismatch)
            {
                results.Add("⚠ Filter mismatch");
            }

            // For lookup columns, don't check datatype - just return lookup results
            return results.Any() ? string.Join(" | ", results) : "✓ OK";
        }

        // Check if has mapping rule (custom logic)
        if (!string.IsNullOrWhiteSpace(mapping.MappingRule) && !mapping.MappingStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
        {
            results.Add("✓ OK (custom mapping rule)");
            return string.Join(" | ", results);
        }

        // Check datatype comparison only if not a lookup and no mapping rule
        var datatypeComp = datatypeComparisons?.FirstOrDefault(d => 
            d.DestinationTable == mapping.NewTableName && d.DestinationColumn == mapping.NewColumn);

        if (datatypeComp != null)
        {
            if (datatypeComp.HasIssues)
            {
                results.Add($"⚠ {string.Join("; ", datatypeComp.Issues.Take(2))}");
            }
            else
            {
                results.Add("✓ Type compatible");
            }
        }

        // Check for null mappings ONLY if column is NOT NULL (explicitly false)
        if ((string.IsNullOrWhiteSpace(mapping.OldColumn) || 
             mapping.OldColumn.Equals("N/A", StringComparison.OrdinalIgnoreCase)) &&
            mapping.NewColumnNullable == false)
        {
            // This is a critical error - trying to insert NULL into NOT NULL column
            results.Add("✗ NULL mapping for NOT NULL column");
        }
        else if ((string.IsNullOrWhiteSpace(mapping.OldColumn) || 
                  mapping.OldColumn.Equals("N/A", StringComparison.OrdinalIgnoreCase)) &&
                 (mapping.NewColumnNullable == true || !mapping.NewColumnNullable.HasValue))
        {
            // Column is nullable or unspecified - this is OK
            if (!results.Any())
            {
                results.Add("✓ Type compatible");
            }
        }

        return results.Any() ? string.Join(" | ", results) : "✓ OK";
    }

    private void GenerateLookupAnalysisSheet(IXLWorksheet sheet, LookupColumnAnalysis lookup)
    {
        if (sheet == null || lookup == null)
            return;

        int row = 1;
        int maxRow = 1048576; // Excel's maximum row limit

        try
        {
            // Sheet title
            sheet.Cell(row, 1).Value = "Lookup Analysis";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Font.FontSize = 14;
            row++;

            // Summary information
            sheet.Cell(row, 1).Value = "Field:";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = $"{lookup.TableName ?? ""}.{lookup.ColumnName ?? ""}";
            row++;

            sheet.Cell(row, 1).Value = "Source:";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = $"{lookup.SourceTable ?? ""}.{lookup.SourceColumn ?? ""}";
            row++;

            if (!string.IsNullOrEmpty(lookup.OldLookupSpec))
            {
                sheet.Cell(row, 1).Value = "Old Lookup Spec:";
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 2).Value = lookup.OldLookupSpec;
                row++;
            }

            if (!string.IsNullOrEmpty(lookup.NewLookupSpec))
            {
                sheet.Cell(row, 1).Value = "New Lookup Spec:";
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 2).Value = lookup.NewLookupSpec;
                row++;
            }

            if (lookup.LookupFilterMismatch)
            {
                sheet.Cell(row, 1).Value = "⚠ Filter Mismatch:";
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.Yellow;
                sheet.Cell(row, 2).Value = lookup.LookupFilterMessage ?? "";
                sheet.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.Yellow;
                row++;
            }

            row++; // Blank row

            // Queries Section
            if (!string.IsNullOrEmpty(lookup.SourceLookupQuery) || 
                !string.IsNullOrEmpty(lookup.DestinationLookupQuery) ||
                !string.IsNullOrEmpty(lookup.AffectedRecordCountQuery))
            {
                sheet.Cell(row, 1).Value = "QUERIES";
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                sheet.Range(row, 1, row, 3).Merge();
                row++;

                if (!string.IsNullOrEmpty(lookup.SourceLookupQuery))
                {
                    sheet.Cell(row, 1).Value = "Source Lookup Query:";
                    sheet.Cell(row, 1).Style.Font.Bold = true;
                    row++;

                    var sourceQueryCell = sheet.Cell(row, 1);
                    sourceQueryCell.Value = lookup.SourceLookupQuery;
                    sourceQueryCell.Style.Font.FontName = "Consolas";
                    sourceQueryCell.Style.Font.FontSize = 9;
                    sourceQueryCell.Style.Fill.BackgroundColor = XLColor.LightGray;
                    sourceQueryCell.Style.Alignment.WrapText = true;
                    sheet.Range(row, 1, row, 3).Merge();
                    row++;
                    row++; // Blank row
                }

                if (!string.IsNullOrEmpty(lookup.DestinationLookupQuery))
                {
                    sheet.Cell(row, 1).Value = "Destination Lookup Query:";
                    sheet.Cell(row, 1).Style.Font.Bold = true;
                    row++;

                    var destQueryCell = sheet.Cell(row, 1);
                    destQueryCell.Value = lookup.DestinationLookupQuery;
                    destQueryCell.Style.Font.FontName = "Consolas";
                    destQueryCell.Style.Font.FontSize = 9;
                    destQueryCell.Style.Fill.BackgroundColor = XLColor.LightGray;
                    destQueryCell.Style.Alignment.WrapText = true;
                    sheet.Range(row, 1, row, 3).Merge();
                    row++;
                    row++; // Blank row
                }

                if (!string.IsNullOrEmpty(lookup.AffectedRecordCountQuery))
                {
                    sheet.Cell(row, 1).Value = "Mismatch Count Query:";
                    sheet.Cell(row, 1).Style.Font.Bold = true;
                    row++;

                    var mismatchQueryCell = sheet.Cell(row, 1);
                    mismatchQueryCell.Value = lookup.AffectedRecordCountQuery;
                    mismatchQueryCell.Style.Font.FontName = "Consolas";
                    mismatchQueryCell.Style.Font.FontSize = 9;
                    mismatchQueryCell.Style.Fill.BackgroundColor = XLColor.LightYellow;
                    mismatchQueryCell.Style.Alignment.WrapText = true;
                    sheet.Range(row, 1, row, 3).Merge();
                    row++;
                    row++; // Blank row
                }

                row++; // Extra blank row after queries section
            }

            // Destination Lookup Values Section
            sheet.Cell(row, 1).Value = "DESTINATION LOOKUP VALUES";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
            sheet.Range(row, 1, row, 3).Merge();
            row++;

            sheet.Cell(row, 1).Value = "Value";
            sheet.Cell(row, 2).Value = "Status";
            sheet.Cell(row, 3).Value = "Notes";
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.LightGray;
            row++;

            if (lookup.DestinationSampleValues != null && lookup.DestinationSampleValues.Any())
            {
                foreach (var value in lookup.DestinationSampleValues)
                {
                    if (row > maxRow - 100) // Leave some buffer
                    {
                        sheet.Cell(row, 1).Value = $"... truncated, too many rows";
                        break;
                    }

                    sheet.Cell(row, 1).Value = value.Value ?? "";
                    sheet.Cell(row, 2).Value = "✓ In Destination";
                    sheet.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.LightGreen;
                    row++;
                }

                if (lookup.DestinationDistinctCount > lookup.DestinationSampleValues.Count)
                {
                    sheet.Cell(row, 1).Value = $"... and {lookup.DestinationDistinctCount - lookup.DestinationSampleValues.Count} more";
                    sheet.Cell(row, 1).Style.Font.Italic = true;
                    row++;
                }
            }
            else
            {
                sheet.Cell(row, 1).Value = "No destination values found";
                sheet.Cell(row, 1).Style.Font.Italic = true;
                row++;
            }

            row++; // Blank row

            // Source Lookup Values Section
            sheet.Cell(row, 1).Value = "SOURCE LOOKUP VALUES";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            sheet.Range(row, 1, row, 3).Merge();
            row++;

            sheet.Cell(row, 1).Value = "Value";
            sheet.Cell(row, 2).Value = "Status";
            sheet.Cell(row, 3).Value = "Notes";
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.LightGray;
            row++;

            if (lookup.SourceSampleValues != null && lookup.SourceSampleValues.Any())
            {
                var destValuesSet = (lookup.DestinationSampleValues?.Values.ToList() ?? new List<string>())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var value in lookup.SourceSampleValues)
                {
                    if (row > maxRow - 100) // Leave some buffer
                    {
                        sheet.Cell(row, 1).Value = $"... truncated, too many rows";
                        break;
                    }

                    sheet.Cell(row, 1).Value = value.Value ?? "";

                    if (destValuesSet.Contains(value.Value))
                    {
                        sheet.Cell(row, 2).Value = "✓ Match Found";
                        sheet.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.LightGreen;
                    }
                    else
                    {
                        sheet.Cell(row, 2).Value = "✗ NOT in Destination";
                        sheet.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.LightPink;
                        sheet.Cell(row, 3).Value = "⚠ Needs mapping or insert";
                    }

                    row++;
                }

                if (lookup.SourceDistinctCount > lookup.SourceSampleValues.Count)
                {
                    sheet.Cell(row, 1).Value = $"... and {lookup.SourceDistinctCount - lookup.SourceSampleValues.Count} more";
                    sheet.Cell(row, 1).Style.Font.Italic = true;
                    row++;
                }
            }
            else
            {
                sheet.Cell(row, 1).Value = "No source values found";
                sheet.Cell(row, 1).Style.Font.Italic = true;
                row++;
            }

            row += 2; // Blank rows

            // Values Mapping Section
            if (lookup.ValuesMapping != null && lookup.ValuesMapping.Any())
            {
                sheet.Cell(row, 1).Value = "VALUES MAPPING";
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
                sheet.Range(row, 1, row, 5).Merge();
                row++;

                // Table headers
                sheet.Cell(row, 1).Value = "Source Code";
                sheet.Cell(row, 2).Value = "Source Value";
                sheet.Cell(row, 3).Value = "Destination Code";
                sheet.Cell(row, 4).Value = "Destination Value";
                sheet.Cell(row, 5).Value = "Status";
                sheet.Range(row, 1, row, 5).Style.Font.Bold = true;
                sheet.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.LightGray;
                row++;

                var matchedCount = 0;
                var missingCount = 0;

                foreach (var valueMap in lookup.ValuesMapping)
                {
                    if (row > maxRow - 100) // Leave some buffer
                    {
                        sheet.Cell(row, 1).Value = $"... truncated, {lookup.ValuesMapping.Count - (row - (lookup.ValuesMapping.Count + row))} more rows";
                        sheet.Range(row, 1, row, 5).Merge();
                        break;
                    }

                    var isMatched = !string.IsNullOrEmpty(valueMap.DestinationLookupValue);

                    sheet.Cell(row, 1).Value = valueMap.SourceLookupCode ?? "";
                    sheet.Cell(row, 2).Value = valueMap.SourceLookupValue ?? "";
                    sheet.Cell(row, 3).Value = isMatched ? (valueMap.DestinationLookupCode ?? "") : "-";
                    sheet.Cell(row, 4).Value = isMatched ? (valueMap.DestinationLookupValue ?? "") : "No match";
                    sheet.Cell(row, 5).Value = isMatched ? "✓ Matched" : "✗ Missing";

                    // Color code the row
                    if (isMatched)
                    {
                        sheet.Cell(row, 5).Style.Fill.BackgroundColor = XLColor.LightGreen;
                        matchedCount++;
                    }
                    else
                    {
                        sheet.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.LightYellow;
                        sheet.Cell(row, 5).Style.Fill.BackgroundColor = XLColor.Yellow;
                        missingCount++;
                    }

                    row++;
                }

                // Summary footer
                sheet.Cell(row, 1).Value = "SUMMARY";
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 2).Value = $"{matchedCount} Matched";
                sheet.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.LightGreen;
                sheet.Cell(row, 3).Value = $"{missingCount} Missing";
                sheet.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.Yellow;

                var totalCount = matchedCount + missingCount;
                var percentage = totalCount > 0 ? (matchedCount * 100.0 / totalCount) : 0;
                sheet.Cell(row, 4).Value = $"{percentage:F1}%";
                sheet.Cell(row, 4).Style.Font.Bold = true;
                sheet.Range(row, 1, row, 5).Style.Font.Bold = true;
                row++;

                row += 2; // Extra blank rows
            }

            // Summary statistics
            sheet.Cell(row, 1).Value = "SUMMARY";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            sheet.Range(row, 1, row, 2).Merge();
            row++;

            sheet.Cell(row, 1).Value = "Total Destination Values:";
            sheet.Cell(row, 2).Value = lookup.DestinationDistinctCount;
            row++;

            sheet.Cell(row, 1).Value = "Total Source Values:";
            sheet.Cell(row, 2).Value = lookup.SourceDistinctCount;
            row++;

            var mismatchCount = lookup.MismatchedValues?.Count ?? 0;
            sheet.Cell(row, 1).Value = "Mismatched Values:";
            sheet.Cell(row, 2).Value = mismatchCount;
            if (mismatchCount > 0)
            {
                sheet.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.Yellow;
            }
            row++;

            // Auto-fit columns
            sheet.Columns().AdjustToContents();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating lookup analysis sheet: {ex.Message}");
            // Add error message to sheet
            try
            {
                sheet.Cell(row + 2, 1).Value = $"Error generating sheet: {ex.Message}";
                sheet.Cell(row + 2, 1).Style.Font.FontColor = XLColor.Red;
            }
            catch
            {
                // Ignore nested errors
            }
        }
    }

    /// <summary>
    /// Add a gray separator row for visual separation between sections
    /// </summary>
    private void AddGraySeparator(IXLWorksheet sheet, int row, int columnCount)
    {
        var range = sheet.Range(row, 1, row, columnCount);
        range.Style.Fill.BackgroundColor = XLColor.Gray;
        range.Merge();

        // Set a small height for the separator
        //sheet.Row(row).Height = 5;
    }

    private string SanitizeSheetName(string name)
    {
        // Excel sheet names can't contain: \ / * ? : [ ]
        // and must be 31 characters or less
        var sanitized = name
            .Replace("\\", "_")
            .Replace("/", "_")
            .Replace("*", "_")
            .Replace("?", "_")
            .Replace(":", "_")
            .Replace("[", "_")
            .Replace("]", "_");

        if (sanitized.Length > 31)
        {
            sanitized = sanitized.Substring(0, 31);
        }

        return sanitized;
    }
}



