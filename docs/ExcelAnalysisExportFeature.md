# Excel Analysis Export Feature

## Overview
The system now supports exporting Excel mapping analysis results to a comprehensive Excel workbook with:
1. **Main DataMapping sheet** with an additional "AnalysisResult" column
2. **Separate tabs for each lookup field** showing both destination and source values side-by-side

## Features

### 1. Main DataMapping Sheet
- Contains all original columns from the mapping template
- **NEW**: `AnalysisResult` column (Column 16) showing:
  - ✓ OK - No issues found
  - ⚠ Lookup warnings with mismatch counts
  - ⚠ Type compatibility warnings
  - ✗ Critical errors (e.g., NULL mapping for NOT NULL columns)
- Color-coded cells:
  - 🟢 **Green** - All checks passed
  - 🟡 **Yellow** - Warnings detected
  - 🔴 **Pink** - Errors found

### 2. Lookup Analysis Tabs
Each lookup field gets its own worksheet with:
- **Tab Name Format**: `{ColumnName}_{SourceTable}`
  - Example: `EmployeeType_OldSystem_Employee`
  - Automatically sanitized to meet Excel constraints (max 31 chars)

#### Tab Structure:

**Section 1: Summary Information**
- Field name (destination table.column)
- Source mapping (source table.column)
- Old Lookup Specification (if present)
- New Lookup Specification (if present)
- Filter Mismatch Warning (if detected)

**Section 2: Destination Lookup Values**
- Header: "DESTINATION LOOKUP VALUES" (green background)
- Columns: Value | Status | Notes
- Shows all values from the new/destination system
- Status: ✓ In Destination

**Section 3: Source Lookup Values**
- Header: "SOURCE LOOKUP VALUES" (blue background)
- Columns: Value | Status | Notes
- Shows all values from the old/source system
- Status indicators:
  - ✓ Match Found (green background) - Value exists in destination
  - ✗ NOT in Destination (pink background) - Value missing, needs mapping

**Section 4: Summary Statistics**
- Total Destination Values count
- Total Source Values count
- Mismatched Values count (highlighted if > 0)

## Usage

### Step 1: Upload Excel Mapping
Upload your Excel file with mapping configuration including lookup specifications:
```
New Lookup Table: [LookupValues].[LookupTypeId] = 1600
Old Lookup Table: [OldLookupValues].[LookupTypeId] = 1500
```

### Step 2: Analyze Mappings
Click "Analyze Excel Mappings" button to:
- Compare datatypes
- Analyze lookup values
- Detect filter mismatches

### Step 3: Export Analysis
Click "Export Analysis to Excel" button to generate the comprehensive Excel report

### Step 4: Review Results
Open the generated Excel file and review:
1. **DataMapping sheet**: Check the AnalysisResult column for any warnings or errors
2. **Lookup tabs**: Review each lookup field's values and identify mismatches

## Example Output

### Main Sheet - AnalysisResult Column Examples:

| Mapping | AnalysisResult |
|---------|---------------|
| Simple mapping | ✓ OK |
| Type mismatch | ⚠ Type: Type conversion needed: VARCHAR → INT |
| Lookup with issues | ⚠ Lookup: 5 mismatched value(s) \| ✓ Type: Compatible |
| NULL mapping | ⚠ NULL mapping (will insert NULL) |
| Critical error | ✗ NULL mapping for NOT NULL column |

### Lookup Tab Example: "StatusCode_Employee"

```
Lookup Analysis
Field:            dbo.Employee.StatusCode
Source:           OldSys.Emp.Status
Old Lookup Spec:  [RefData].[CategoryId] = 50
New Lookup Spec:  [RefData].[CategoryId] = 100

⚠ Filter Mismatch: Lookup filter mismatch: Old=50, New=100

DESTINATION LOOKUP VALUES
Value    | Status              | Notes
---------|---------------------|------
Active   | ✓ In Destination    |
Inactive | ✓ In Destination    |
Pending  | ✓ In Destination    |

SOURCE LOOKUP VALUES
Value    | Status                  | Notes
---------|-------------------------|-------------------------
Active   | ✓ Match Found           |
Inactive | ✓ Match Found           |
OnHold   | ✗ NOT in Destination    | ⚠ Needs mapping or insert
Archived | ✗ NOT in Destination    | ⚠ Needs mapping or insert

SUMMARY
Total Destination Values:  3
Total Source Values:        4
Mismatched Values:          2  (highlighted in yellow)
```

## Analysis Result Interpretations

### ✓ OK
All checks passed:
- No datatype issues
- No lookup mismatches  
- Valid mapping configured

### ⚠ Warnings
Issues that may need attention:
- **Lookup mismatches**: Some source values don't exist in destination
- **Type conversions**: Implicit conversion may occur
- **NULL mappings**: Column will be populated with NULL values
- **Filter mismatches**: Different filter values in old vs new lookup specs

### ✗ Errors  
Critical issues requiring action:
- **NULL for NOT NULL**: Cannot insert NULL into non-nullable column
- **Type incompatibility**: Cannot convert between types without explicit handling

## File Naming Convention
```
MappingAnalysis_{yyyyMMdd_HHmmss}.xlsx
```
Example: `MappingAnalysis_20260122_235959.xlsx`

## Benefits

✅ **Comprehensive Documentation** - All analysis results in one Excel file  
✅ **Visual Indicators** - Color-coded cells for quick issue identification  
✅ **Detailed Lookup Analysis** - Separate tabs for each lookup field  
✅ **Value-by-Value Comparison** - See exactly which values match/mismatch  
✅ **Actionable Insights** - Notes column suggests actions needed  
✅ **Easy Sharing** - Excel format for team collaboration  
✅ **Audit Trail** - Document analysis results for compliance  

## Best Practices

1. **Review All Tabs**: Check both the main sheet and each lookup tab
2. **Address Errors First**: Fix any ✗ errors before proceeding
3. **Investigate Warnings**: Review all ⚠ warnings and document decisions
4. **Update Mappings**: Use insights to refine your Excel mapping file
5. **Re-analyze**: After updates, re-upload and analyze again
6. **Archive Results**: Keep analysis Excel files for documentation

## Troubleshooting

### "No analysis results available to export"
- Must click "Analyze Excel Mappings" first
- Ensure connections are tested successfully
- Upload a valid mapping Excel file

### Lookup tab shows "No values found"
- Check lookup specification syntax: `[TableName].[ColumnName] = Value`
- Verify the lookup table exists in the database
- Ensure the filter value is correct and data exists

### Sheet name too long
- Excel sheet names limited to 31 characters
- System automatically truncates and sanitizes
- Review the actual tab name in the exported file
