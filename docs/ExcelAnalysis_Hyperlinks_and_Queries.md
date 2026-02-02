# Excel Analysis Export - Hyperlinks and Queries Enhancement

## Overview
Enhanced the Excel analysis export with clickable hyperlinks to lookup sheets and detailed SQL queries for validation and troubleshooting.

## New Features

### 1. Hyperlinks in Main DataMapping Sheet

**AnalysisResult Column with Links:**
- Lookup columns now have **clickable hyperlinks** (🔗 indicator)
- Click the link to jump directly to the detailed lookup analysis tab
- Link text is **blue and underlined** following Excel conventions
- Background color still indicates status (green/yellow/pink)

**Example:**
```
AnalysisResult Column:
✓ Lookup values match 🔗        (Green background, clickable)
⚠ 5 mismatched value(s) 🔗     (Yellow background, clickable)
```

**Navigation:**
1. Click on any cell with 🔗 in the AnalysisResult column
2. Excel jumps to the corresponding lookup analysis tab
3. View detailed value comparisons and queries

### 2. SQL Queries in Lookup Analysis Tabs

Each lookup analysis tab now includes a **QUERIES** section with:

#### A. Source Lookup Query
- Shows the exact SQL used to retrieve source values
- Formatted in monospace font (Consolas)
- Gray background for easy identification
- Copy-paste ready for manual validation

**Example:**
```sql
SELECT * FROM [OldLookupValues] WHERE [LookupTypeId] = 1500
```

#### B. Destination Lookup Query  
- Shows the exact SQL used to retrieve destination values
- Same formatting as source query
- Useful for verifying lookup configuration

**Example:**
```sql
SELECT * FROM [LookupValues] WHERE [LookupTypeId] = 1600
```

#### C. Mismatch Count Query
- SQL to count records affected by mismatched values
- Yellow background to highlight importance
- Groups by value with record counts
- Critical for impact analysis

**Example:**
```sql
SELECT [OldColumn], [Value], COUNT(*) as RecordCount
FROM [SourceTable]
INNER JOIN [LookupTable] ON ...
WHERE [Value] IN ('MismatchedValue1', 'MismatchedValue2')
GROUP BY [OldColumn], [Value]
ORDER BY RecordCount DESC
```

## Usage Guide

### Navigating with Hyperlinks

1. **Export Analysis:**
   ```
   Click "Export Analysis to Excel" button
   ```

2. **Open DataMapping Tab:**
   - Review AnalysisResult column
   - Look for 🔗 link indicators

3. **Click Hyperlink:**
   - Excel jumps to lookup details tab
   - No need to manually search for tabs

4. **Return to Main:**
   - Use Excel's navigation (Ctrl+Home)
   - Or click sheet tabs at bottom

### Using SQL Queries

#### Validation Workflow:
1. **Open Lookup Tab:**
   - Via hyperlink or manual navigation

2. **Review QUERIES Section:**
   - Located after summary info
   - Before value comparisons

3. **Copy Source Query:**
   ```sql
   -- Run in source database
   SELECT * FROM [OldLookupValues] 
   WHERE [LookupTypeId] = 1500
   ```

4. **Copy Destination Query:**
   ```sql
   -- Run in destination database
   SELECT * FROM [LookupValues] 
   WHERE [LookupTypeId] = 1600
   ```

5. **Compare Results:**
   - Verify counts match Excel
   - Check for missing values
   - Validate filter logic

#### Troubleshooting Mismatches:
1. **Copy Mismatch Query:**
   ```sql
   -- Run in source database
   SELECT [Status], COUNT(*) as RecordCount
   FROM [Employee]
   WHERE [Status] IN ('OnHold', 'Archived')
   GROUP BY [Status]
   ORDER BY RecordCount DESC
   ```

2. **Identify Impact:**
   - See which values cause issues
   - Count affected records
   - Prioritize fixes

3. **Take Action:**
   - Insert missing lookup values
   - Update mapping logic
   - Document exceptions

## Excel Layout

### Main DataMapping Sheet
```
| New Table | New Column | ... | AnalysisResult              |
|-----------|------------|-----|-----------------------------|
| Employee  | Status     | ... | ✓ Lookup values match 🔗    |
| Employee  | Type       | ... | ⚠ 3 mismatched value(s) 🔗 |
| Person    | Name       | ... | ✓ Type compatible           |
```

### Lookup Analysis Tab (Status_Employee)
```
Lookup Analysis
Field:           Employee.Status
Source:          OldEmployee.EmpStatus
Old Lookup Spec: [OldLookupValues].[LookupTypeId] = 1500
New Lookup Spec: [LookupValues].[LookupTypeId] = 1600

QUERIES
─────────────────────────────────────────────────────
Source Lookup Query:
SELECT * FROM [OldLookupValues] WHERE [LookupTypeId] = 1500

Destination Lookup Query:
SELECT * FROM [LookupValues] WHERE [LookupTypeId] = 1600

Mismatch Count Query:
SELECT [Status], COUNT(*) as RecordCount
FROM [OldEmployee]
WHERE [Status] IN ('OnHold', 'Archived')
GROUP BY [Status]
ORDER BY RecordCount DESC

DESTINATION LOOKUP VALUES
─────────────────────────────────────────────────────
Value     | Status              | Notes
Active    | ✓ In Destination    |
Inactive  | ✓ In Destination    |
Pending   | ✓ In Destination    |

SOURCE LOOKUP VALUES
─────────────────────────────────────────────────────
Value     | Status                | Notes
Active    | ✓ Match Found         |
Inactive  | ✓ Match Found         |
OnHold    | ✗ NOT in Destination  | ⚠ Needs mapping or insert
Archived  | ✗ NOT in Destination  | ⚠ Needs mapping or insert

SUMMARY
─────────────────────────────────────────────────────
Total Destination Values: 3
Total Source Values:      4
Mismatched Values:        2
```

## Implementation Details

### Hyperlink Creation
```csharp
// Create internal Excel hyperlink
cell.SetHyperlink(new XLHyperlink($"'{lookupSheetName}'!A1"));
cell.Style.Font.FontColor = XLColor.Blue;
cell.Style.Font.Underline = XLFontUnderlineValues.Single;
```

### Query Formatting
```csharp
// Monospace font for SQL
queryCell.Style.Font.FontName = "Consolas";
queryCell.Style.Font.FontSize = 9;
queryCell.Style.Alignment.WrapText = true;

// Color coding
sourceQuery.Style.Fill.BackgroundColor = XLColor.LightGray;
mismatchQuery.Style.Fill.BackgroundColor = XLColor.LightYellow;
```

### Sheet Name Tracking
```csharp
// Dictionary to map columns to sheet names
var lookupSheetNames = new Dictionary<string, string>();
// Key: "TableName.ColumnName"
// Value: "ActualSheetName"
```

## Benefits

### For Data Analysts
✅ **Quick Navigation** - Jump to details with one click  
✅ **SQL Validation** - Copy queries to verify results  
✅ **Impact Analysis** - Run mismatch queries for counts  
✅ **Documentation** - Queries serve as audit trail  

### For Developers
✅ **Debugging** - Understand exactly what queries ran  
✅ **Troubleshooting** - Reproduce issues with exact SQL  
✅ **Testing** - Validate query logic independently  
✅ **Learning** - See how filters are applied  

### For Project Managers
✅ **Progress Tracking** - Click through all lookups quickly  
✅ **Risk Assessment** - Mismatch queries show impact  
✅ **Communication** - Share queries with DBA team  
✅ **Compliance** - Documented validation process  

## Troubleshooting

### Hyperlink Not Working
**Issue:** Click doesn't navigate  
**Solution:**
- Ensure lookup sheet exists
- Check sheet name matches (case-sensitive)
- Verify Excel security settings

### Query Returns Different Results
**Issue:** Manual run shows different data  
**Solution:**
- Check database connection (source vs destination)
- Verify data hasn't changed since analysis
- Confirm filter values in query
- Check for case sensitivity

### Queries Section Missing
**Issue:** Tab doesn't show queries  
**Solution:**
- Lookup might not have specifications
- Check if SourceLookupQuery populated
- Fallback lookup analysis doesn't generate queries

## Future Enhancements

Potential improvements:
- [ ] Add "Run Query" button (macro-enabled workbooks)
- [ ] Include execution timestamps
- [ ] Add query performance metrics
- [ ] Generate data fix SQL (INSERT statements)
- [ ] Link to source table schema
- [ ] Add query result preview

## Example Scenarios

### Scenario 1: Quick Impact Check
1. Open exported Excel
2. Scan AnalysisResult for yellow/pink
3. Click hyperlink on problematic lookup
4. Copy mismatch count query
5. Run in source database
6. Get exact record count per value
7. Prioritize data fixes

### Scenario 2: Validate Filter Logic
1. Navigate to lookup tab
2. Review lookup specifications
3. Copy source query
4. Run in source database
5. Compare result count with Excel
6. Verify filter is correct
7. Adjust Excel mapping if needed

### Scenario 3: Document for DBA
1. Export analysis Excel
2. Share file with database team
3. They can see exact queries used
4. No ambiguity about filter values
5. Easy to reproduce and verify
6. Clear action items

## Related Files
- `src\ZaDataStudio.Infrastructure\Excel\ExcelMappingService.cs`
- `src\ZaDataStudio.Domain\Entities\LookupColumnAnalysis.cs`
- `src\ZaDataStudio.Application\Mapping\MappingComparisonService.cs`
