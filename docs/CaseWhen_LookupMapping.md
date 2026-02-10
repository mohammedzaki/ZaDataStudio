# CASE WHEN Lookup Mapping SQL Generation

## Overview
Enhanced the `MappingRuleEngine` to generate efficient CASE WHEN SQL statements for lookup mappings using the ValuesMapping data from lookup analysis. This eliminates the need for JOIN statements and creates more readable, maintainable SQL code.

## Changes Made

### 1. Updated `GenerateMigrationSQL` Method
**File:** `src\ZaDataStudio.Application\Mapping\MappingRuleEngine.cs`

- Now passes `analysisResult` parameter through to `GenerateTableMigrationSQL`
- Enables lookup-aware SQL generation

### 2. Enhanced `GenerateTableMigrationSQL` Method

**New Logic:**
1. For each column mapping, check if lookup analysis with ValuesMapping exists
2. If yes, generate CASE WHEN statement
3. If no, use regular rule engine processing

```csharp
// Check if this mapping has lookup analysis with ValuesMapping
var lookupAnalysis = analysisResult?.LookupAnalysis?.FirstOrDefault(la =>
    la.TableName == mapping.NewTableName && 
    la.ColumnName == mapping.NewColumn &&
    la.ValuesMapping != null && 
    la.ValuesMapping.Any());

if (lookupAnalysis != null)
{
    // Generate CASE WHEN statement from ValuesMapping
    sqlExpression = GenerateLookupCaseWhen(mapping, lookupAnalysis);
}
else
{
    // Use regular rule engine
    var result = ProcessMapping(mapping, context);
    sqlExpression = result.SqlExpression;
}
```

### 3. Added `GenerateLookupCaseWhen` Method

**Purpose:** Generate CASE WHEN SQL from ValuesMapping data

**Features:**
- Handles matched values only (excludes unmapped values)
- Automatically detects numeric vs string source codes
- Properly escapes SQL string literals
- Handles NULL values
- Adds comments about unmapped values

**Example Output:**
```sql
CASE
    WHEN src.[CategoryCode] = 1 THEN '201'
    WHEN src.[CategoryCode] = 2 THEN '202'
    WHEN src.[CategoryCode] = 3 THEN '203'
    WHEN src.[CategoryCode] = 5 THEN '205'
    ELSE NULL -- Unmapped value (1 unmapped value(s))
END
```

### 4. Added Helper Methods

#### `EscapeSql(string value)`
- Escapes single quotes in SQL string literals
- Prevents SQL injection
- Example: `O'Brien` → `O''Brien`

#### `IsNumeric(string value)`
- Determines if a value is numeric
- Decides whether to quote values in WHEN clauses
- Supports int, long, and decimal types

## Benefits

### 1. **No JOIN Required**
**Before (JOIN-based):**
```sql
SELECT lv.[Name]
FROM SourceTable AS src
LEFT JOIN [dbo].[LookupValues] AS lv
    ON lv.[Code] = src.[CategoryCode]
    AND lv.[TypeId] = 1600
```

**After (CASE WHEN):**
```sql
SELECT 
    CASE
        WHEN src.[CategoryCode] = 1 THEN '201'
        WHEN src.[CategoryCode] = 2 THEN '202'
        WHEN src.[CategoryCode] = 3 THEN '203'
        ELSE NULL
    END AS [CategoryId]
FROM SourceTable AS src
```

### 2. **Better Performance**
- No additional table scan
- No JOIN overhead
- Faster execution for small lookup tables
- More efficient for in-memory processing

### 3. **More Readable**
- Clear mapping visibility in SQL
- Easy to understand transformations
- Self-documenting code
- No need to reference external lookup tables

### 4. **Easier Maintenance**
- All mappings in one place
- No dependency on lookup table structure
- Can be easily modified
- Version control friendly

### 5. **Safer**
- Only maps known values
- Unmapped values return NULL with comment
- No risk of missing lookup data
- Explicit value mapping

## Usage Example

### Scenario: Social Media Platform Mapping

**Source Data:**
| CategoryCode | CategoryName |
|--------------|--------------|
| 1 | Facebook |
| 2 | Twitter |
| 3 | Instagram |
| 5 | YouTube |

**Destination Lookup:**
| PlatformId | PlatformName |
|------------|--------------|
| 201 | Facebook |
| 202 | Twitter |
| 203 | Instagram |
| 205 | YouTube |

**ValuesMapping:**
```
1 (Facebook) → 201 (Facebook) ✓
2 (Twitter) → 202 (Twitter) ✓
3 (Instagram) → 203 (Instagram) ✓
4 (LinkedIn) → NULL ✗ (No match)
5 (YouTube) → 205 (YouTube) ✓
```

**Generated SQL:**
```sql
INSERT INTO [dbo].[SocialMediaLinks] (
    [LinkId],
    [PlatformId],
    [URL]
)
SELECT
    src.[LinkId] AS [LinkId],
    CASE
        WHEN src.[CategoryCode] = 1 THEN '201'
        WHEN src.[CategoryCode] = 2 THEN '202'
        WHEN src.[CategoryCode] = 3 THEN '203'
        WHEN src.[CategoryCode] = 5 THEN '205'
        ELSE NULL -- Unmapped value (1 unmapped value(s))
    END AS [PlatformId],
    src.[LinkURL] AS [URL]
FROM [dbo].[SourceLinks] AS src
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[SocialMediaLinks] dest
    WHERE dest.[LinkId] = src.[LinkId]
);
```

## Technical Details

### Value Type Detection

**Numeric Values** (no quotes):
```sql
WHEN src.[Code] = 123 THEN '456'
```

**String Values** (with quotes):
```sql
WHEN src.[Code] = 'ABC' THEN 'XYZ'
```

**NULL Values** (special handling):
```sql
WHEN src.[Code] IS NULL THEN 'N/A'
```

### SQL Injection Prevention

All string values are properly escaped:
```csharp
private string EscapeSql(string value)
{
    return value.Replace("'", "''");
}
```

**Example:**
- Input: `O'Brien`
- Output: `O''Brien`
- SQL: `WHEN src.[Name] = 'O''Brien' THEN 'USER123'`

### Unmapped Values Handling

If ValuesMapping contains unmapped values (DestinationLookupValue is empty):
- They are excluded from WHEN clauses
- ELSE NULL is used as fallback
- Comment indicates count of unmapped values
- Prevents errors from missing mappings

## Integration

### In Migration SQL Generation

```csharp
// Called automatically by MappingRuleEngine
var migrationSql = _ruleEngine.GenerateMigrationSQL(
    config,
    analysisResult,  // Contains ValuesMapping
    datatypeComparisons,
    includeTransaction: true
);
```

### Analysis Result Flow

1. **Analysis Phase** (LookupColumnAnalyzer):
   - Compares source and destination values
   - Populates ValuesMapping with matched pairs
   - Identifies unmapped values

2. **SQL Generation Phase** (MappingRuleEngine):
   - Checks for ValuesMapping
   - Generates CASE WHEN if available
   - Falls back to rule engine if not

## Performance Comparison

### Small Lookup Tables (< 100 values)
- **CASE WHEN**: Faster (no JOIN overhead)
- **Recommended**: Use CASE WHEN

### Medium Lookup Tables (100-1000 values)
- **CASE WHEN**: Comparable performance
- **JOIN**: Slightly better with proper indexes
- **Recommended**: Use CASE WHEN for readability

### Large Lookup Tables (> 1000 values)
- **CASE WHEN**: May impact query plan
- **JOIN**: Better performance with indexes
- **Recommended**: Consider JOIN or indexed views

## Limitations

### 1. SQL Length
Very large CASE statements may exceed SQL Server limits:
- Maximum SQL batch size: 65,536 KB
- Maximum nested CASE levels: 10
- **Solution**: Use JOIN for > 1000 mappings

### 2. Query Plan
Complex CASE statements may affect query optimization:
- SQL Server may not optimize as well as JOINs
- **Solution**: Test query plans for large datasets

### 3. Dynamic Updates
CASE statements are static:
- Lookup changes require SQL regeneration
- **Solution**: Use JOIN for frequently changing lookups

## Best Practices

### 1. **Use CASE WHEN When:**
- Lookup table has < 100 values
- Values rarely change
- Readability is priority
- No indexes on lookup table
- Migration is one-time

### 2. **Use JOIN When:**
- Lookup table has > 1000 values
- Values change frequently
- Performance is critical
- Proper indexes exist
- Ongoing synchronization

### 3. **Hybrid Approach:**
- Small, static lookups: CASE WHEN
- Large, dynamic lookups: JOIN
- Medium lookups: Test and decide

## Testing

### Unit Tests

```csharp
[Fact]
public void GenerateLookupCaseWhen_WithMappedValues_GeneratesCorrectSQL()
{
    // Arrange
    var mapping = new DataColumnMapping 
    { 
        OldTableName = "SourceTable",
        OldColumn = "CategoryCode",
        NewColumn = "CategoryId"
    };
    
    var lookupAnalysis = new LookupColumnAnalysis
    {
        ValuesMapping = new List<LookupValueMapping>
        {
            new() { SourceLookupCode = "1", DestinationLookupCode = "201" },
            new() { SourceLookupCode = "2", DestinationLookupCode = "202" }
        }
    };
    
    // Act
    var sql = engine.GenerateLookupCaseWhen(mapping, lookupAnalysis);
    
    // Assert
    Assert.Contains("CASE", sql);
    Assert.Contains("WHEN src.[CategoryCode] = 1 THEN '201'", sql);
    Assert.Contains("ELSE NULL", sql);
    Assert.Contains("END", sql);
}
```

### Integration Tests

1. Test with numeric source codes
2. Test with string source codes
3. Test with NULL values
4. Test with unmapped values
5. Test SQL injection prevention
6. Test large lookup tables
7. Test empty ValuesMapping

## Future Enhancements

1. **Adaptive Selection**: Automatically choose CASE WHEN vs JOIN based on value count
2. **Batch Optimization**: Split large CASE statements into multiple queries
3. **Indexed CASE**: Generate indexed views for complex CASE statements
4. **Parameterization**: Use parameters for frequently used values
5. **Statistics**: Track performance metrics for each approach
6. **Hybrid Queries**: Combine CASE WHEN and JOIN for optimal performance

## Related Files

- `src\ZaDataStudio.Application\Mapping\MappingRuleEngine.cs` - Main implementation
- `src\ZaDataStudio.Application\Mapping\LookupColumnAnalyzer.cs` - ValuesMapping generation
- `src\ZaDataStudio.Domain\Entities\LookupColumnAnalysis.cs` - Data model
- `docs\ValuesMapping_Feature.md` - ValuesMapping feature doc
- `docs\LookupMappingRule_JoinBased.md` - JOIN-based approach doc

## Conclusion

The CASE WHEN approach provides a cleaner, more maintainable SQL generation strategy for lookup mappings. By leveraging the ValuesMapping data from analysis, we can generate efficient, self-documenting SQL that doesn't require JOIN operations. This is ideal for most scenarios with small to medium lookup tables and provides excellent readability and performance.
