# Lookup Table Mapping Feature

## Overview
The Excel mapping service now supports advanced lookup table specifications with filtered values, allowing you to compare lookup values between old and new systems.

## Format Specification

### Lookup Column Format
```
[TableName].[ColumnName] = FilterValue
```

### Examples
```
[LookupValues].[LookupTypeId] = 1600
[RefData].[CategoryId] = 5
[dbo.SystemLookups].[TypeCode] = 'EMP'
```

## Excel Columns

### Column 6: New Lookup Table
Specify the destination system's lookup table filter:
- **Format**: `[TableName].[ColumnName] = Value`
- **Example**: `[LookupValues].[LookupTypeId] = 1600`
- **Purpose**: Filters lookup values in the new/destination system

### Column 12: Old Lookup Table  
Specify the source system's lookup table filter:
- **Format**: `[TableName].[ColumnName] = Value`
- **Example**: `[OldLookupValues].[LookupTypeId] = 1500`
- **Purpose**: Filters lookup values in the old/source system

## How It Works

### 1. Upload Excel with Lookup Specifications
```excel
| New Table | New Column    | ... | New Lookup Table                     | ... | Old Lookup Table                        |
|-----------|---------------|-----|--------------------------------------|-----|-----------------------------------------|
| Employee  | EmployeeType  | ... | [LookupValues].[LookupTypeId] = 1600 | ... | [OldLookupValues].[LookupTypeId] = 1500 |
```

### 2. Click "Analyze Excel Mappings"
The system will:
1. Parse both Old and New lookup specifications
2. Query source database using Old lookup filter
3. Query destination database using New lookup filter
4. Compare distinct values between both systems
5. Identify mismatched values
6. Check if filter values differ

### 3. Review Analysis Results
The analysis will show:
- **Lookup Filter Specifications**: Both old and new filters displayed
- **Filter Mismatch Warning**: If filter values differ (e.g., 1500 vs 1600)
- **Distinct Value Counts**: Number of unique values in each system
- **Sample Values**: Top 5 values from each system
- **Mismatched Values**: Values in source but not in destination

## Example Scenario

### Excel Configuration
```
New Table: dbo.Employee
New Column: StatusCode
New Lookup Table: [RefData].[CategoryId] = 100
Old Table: OldSys.Emp
Old Column: Status  
Old Lookup Table: [OldRefData].[CategoryId] = 50
```

### What Happens
1. **Source Query**: 
   ```sql
   SELECT DISTINCT Status 
   FROM OldRefData 
   WHERE CategoryId = 50
   ```

2. **Destination Query**:
   ```sql
   SELECT DISTINCT StatusCode
   FROM RefData
   WHERE CategoryId = 100
   ```

3. **Comparison**: 
   - Compares values from both queries
   - Identifies values in old system not present in new system
   - Warns if filter values (50 vs 100) differ

## SQL Generation

When generating migration SQL, the lookup specification is used to create filtered queries:

```sql
INSERT INTO [dbo].[Employee] ([StatusCode])
SELECT 
    (SELECT TOP 1 lv.[Status]
     FROM [OldRefData] AS lv
     WHERE lv.[CategoryId] = 50
       AND lv.[SomeKeyColumn] = os.[Status]) AS [StatusCode]
FROM [OldSys].[Emp] AS os;
```

## Benefits

✅ **Filtered Comparisons** - Only compare relevant lookup values  
✅ **Multi-System Support** - Different filters for old vs new systems  
✅ **Mismatch Detection** - Identifies incompatible lookup values  
✅ **Clear Documentation** - Specifications embedded in Excel  
✅ **Automated Validation** - No manual value checking required

## Best Practices

1. **Use Consistent Format**: Always use `[TableName].[ColumnName] = Value`
2. **Include Schema**: Use `[dbo.TableName]` format when needed
3. **Quote Strings**: Use single quotes for string values: `= 'VALUE'`
4. **Document Filters**: Explain filter logic in Notes column
5. **Test Both Systems**: Ensure filter values exist in both databases

## Troubleshooting

### "No distinct values found"
- Check if the filter column and value exist in the table
- Verify connection strings are correct
- Ensure the lookup table has data

### "Filter mismatch warning"
- Review if different filter values are intentional
- Update mappings if filters should match
- Document reason for different filters in Notes column

### "Values not found in destination"
- May require data migration for lookup tables first
- Check if lookup values were renamed
- Consider adding INSERT statements for missing lookups
