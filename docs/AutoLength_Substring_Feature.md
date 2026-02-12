# Auto-Length SUBSTRING Feature

## Overview
The SubstringMappingRule now supports automatic length detection from the destination column's data type. When you specify just `SUBSTRING` without parameters, the rule automatically generates a SUBSTRING expression using the destination column's maximum length.

## How It Works

### Formula
```
Mapping Rule: SUBSTRING
Old Column: SourceColumnName
New DataType: VARCHAR(N)

Generated SQL: SUBSTRING(src.[SourceColumnName], 1, N)
```

### Detection Logic
1. Check if Mapping Rule is exactly "SUBSTRING"
2. Extract max length from New DataType
3. Use Old Column as source
4. Generate: `SUBSTRING(alias.[OldColumn], 1, maxLength)`

## Data Type Parsing

### Supported Formats

| New DataType | Extracted Length | Generated SQL |
|--------------|------------------|---------------|
| `VARCHAR(50)` | 50 | `SUBSTRING(src.[Col], 1, 50)` |
| `NVARCHAR(100)` | 100 | `SUBSTRING(src.[Col], 1, 100)` |
| `CHAR(20)` | 20 | `SUBSTRING(src.[Col], 1, 20)` |
| `NCHAR(10)` | 10 | `SUBSTRING(src.[Col], 1, 10)` |
| `VARCHAR(MAX)` | NULL → 255 | `LEFT(src.[Col], 255)` with warning |
| `NVARCHAR(MAX)` | NULL → 255 | `LEFT(src.[Col], 255)` with warning |
| `INT` | NULL → 255 | `LEFT(src.[Col], 255)` with warning |
| *(empty)* | NULL → 255 | `LEFT(src.[Col], 255)` with warning |

### Fallback Behavior
If max length can't be determined:
- Uses `LEFT(column, 255)` as safe default
- Adds comment: `-- No max length specified, using 255`

## Excel Mapping Examples

### Example 1: Simple Truncation

**Scenario:** Product descriptions are too long for new system

**Excel:**
| New Table | New Column | New DataType | Old Table | Old Column | Mapping Rule |
|-----------|-----------|--------------|-----------|------------|--------------|
| Products | Description | VARCHAR(200) | OldProducts | LongDescription | `SUBSTRING` |

**Generated SQL:**
```sql
SUBSTRING(src.[LongDescription], 1, 200) AS [Description]
```

**Result:**
- Source: "This is a very long product description with lots of details..." (500 chars)
- Dest: "This is a very long product description with lots of details..." (200 chars)

---

### Example 2: Multiple Truncations

**Excel:**
| New Column | New DataType | Old Column | Mapping Rule |
|-----------|--------------|------------|--------------|
| ProductName | NVARCHAR(100) | FullProductName | `SUBSTRING` |
| CategoryName | VARCHAR(50) | FullCategoryName | `SUBSTRING` |
| BrandName | VARCHAR(30) | FullBrandName | `SUBSTRING` |

**Generated SQL:**
```sql
SELECT
    SUBSTRING(src.[FullProductName], 1, 100) AS [ProductName],
    SUBSTRING(src.[FullCategoryName], 1, 50) AS [CategoryName],
    SUBSTRING(src.[FullBrandName], 1, 30) AS [BrandName]
FROM [dbo].[OldProducts] AS src;
```

---

### Example 3: Mixed with Explicit Substring

**Excel:**
| New Column | New DataType | Old Column | Mapping Rule | Notes |
|-----------|--------------|------------|--------------|-------|
| Title | VARCHAR(100) | LongTitle | `SUBSTRING` | Auto-truncate to 100 |
| SKUPrefix | VARCHAR(3) | SKU | `LEFT(SKU, 3)` | Explicit: first 3 |
| SKUSuffix | VARCHAR(4) | SKU | `RIGHT(SKU, 4)` | Explicit: last 4 |

**Generated SQL:**
```sql
SELECT
    SUBSTRING(src.[LongTitle], 1, 100) AS [Title],      -- Auto-length
    LEFT(src.[SKU], 3) AS [SKUPrefix],                  -- Explicit
    RIGHT(src.[SKU], 4) AS [SKUSuffix]                  -- Explicit
FROM [dbo].[Products] AS src;
```

## Benefits

### 1. **Automatic Adaptation**
**Before:**
```
New DataType: VARCHAR(100)
Mapping Rule: LEFT(Description, 100)
↓ Later change data type
New DataType: VARCHAR(150)
Mapping Rule: LEFT(Description, 100)  ← Still using old length!
```

**After:**
```
New DataType: VARCHAR(100)
Mapping Rule: SUBSTRING
↓ Later change data type
New DataType: VARCHAR(150)
Mapping Rule: SUBSTRING  ← Automatically uses 150!
```

### 2. **Prevents Truncation Errors**
Ensures substring length never exceeds destination column size.

### 3. **Less Manual Calculation**
No need to count characters or remember data type sizes.

### 4. **Self-Documenting**
`SUBSTRING` in mapping rule clearly indicates "truncate to fit destination".

### 5. **Easier Maintenance**
Change data type in one place (New DataType column), mapping rule adapts automatically.

## When to Use Auto-Length vs Explicit Length

### Use Auto-Length (SUBSTRING) When:
- ✅ Truncating to fit destination column
- ✅ Don't care about exact length, just "make it fit"
- ✅ Destination column size may change
- ✅ Migrating legacy data with oversized fields

**Example:**
```
Old: Comments VARCHAR(5000)
New: Notes VARCHAR(500)
Rule: SUBSTRING  ← Perfect use case
```

### Use Explicit Length When:
- ✅ Extracting specific part of data (area code, SKU prefix)
- ✅ Length has business meaning (first 3 = department code)
- ✅ Position matters (characters 5-7 = category)
- ✅ Length is independent of destination size

**Example:**
```
Old: Phone VARCHAR(50) = "555-123-4567"
New: AreaCode VARCHAR(3)
Rule: LEFT(Phone, 3)  ← Must be exactly 3
```

## Comparison Table

| Scenario | Use Auto-Length | Use Explicit | Reason |
|----------|-----------------|--------------|--------|
| Truncate to fit | ✅ `SUBSTRING` | ❌ | Adapts to schema changes |
| Extract area code | ❌ | ✅ `LEFT(Phone, 3)` | Fixed business rule |
| Shorten descriptions | ✅ `SUBSTRING` | ❌ | Just needs to fit |
| Parse SKU format | ❌ | ✅ `SUBSTRING(SKU, 1, 3)` | Fixed structure |
| Database migration | ✅ `SUBSTRING` | ❌ | Schema may evolve |
| Extract initials | ❌ | ✅ `LEFT(Name, 1)` | Always 1 character |

## Testing

### Unit Tests

```csharp
[Theory]
[InlineData("VARCHAR(50)", 50)]
[InlineData("NVARCHAR(100)", 100)]
[InlineData("CHAR(20)", 20)]
[InlineData("NCHAR(10)", 10)]
public void ExtractMaxLength_ValidDataType_ReturnsLength(string dataType, int expected)
{
    var rule = new SubstringMappingRule();
    var length = rule.ExtractMaxLength(dataType);
    
    Assert.Equal(expected, length);
}

[Theory]
[InlineData("VARCHAR(MAX)")]
[InlineData("NVARCHAR(MAX)")]
[InlineData("INT")]
[InlineData("")]
public void ExtractMaxLength_NoLength_ReturnsNull(string dataType)
{
    var rule = new SubstringMappingRule();
    var length = rule.ExtractMaxLength(dataType);
    
    Assert.Null(length);
}

[Fact]
public void Apply_SubstringOnly_UsesDestMaxLength()
{
    var rule = new SubstringMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = "SUBSTRING",
        OldTableName = "dbo.Products",
        OldColumn = "LongName",
        NewColumn = "ShortName",
        NewDataType = "VARCHAR(50)"
    };
    
    var result = rule.Apply(mapping, new MappingContext());
    
    Assert.Equal("SUBSTRING(src.[LongName], 1, 50)", result.SqlExpression);
}

[Fact]
public void Apply_SubstringOnly_NoMaxLength_UsesFallback()
{
    var rule = new SubstringMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = "SUBSTRING",
        OldTableName = "dbo.Products",
        OldColumn = "Description",
        NewColumn = "ShortDesc",
        NewDataType = "VARCHAR(MAX)"
    };
    
    var result = rule.Apply(mapping, new MappingContext());
    
    Assert.Contains("LEFT(src.[Description], 255)", result.SqlExpression);
    Assert.Contains("No max length specified", result.SqlExpression);
}
```

### Integration Tests

```csharp
[Fact]
public async Task MigrationSQL_WithAutoSubstring_GeneratesCorrectly()
{
    var config = new DataMappingConfiguration
    {
        ColumnMappings = new List<DataColumnMapping>
        {
            new()
            {
                NewTableName = "Products",
                NewColumn = "ShortName",
                NewDataType = "NVARCHAR(50)",
                OldTableName = "OldProducts",
                OldColumn = "ProductName",
                MappingRule = "SUBSTRING",
                MappingStatus = "Approved"
            }
        }
    };
    
    var sql = _engine.GenerateMigrationSQL(config, null, null);
    
    Assert.Contains("SUBSTRING(src.[ProductName], 1, 50) AS [ShortName]", sql);
}
```

## Practical Use Cases

### Use Case 1: Legacy Database with Oversized Columns

**Problem:**
```
Old System: Description VARCHAR(8000) (way too large)
New System: Notes VARCHAR(500) (more reasonable)
```

**Solution:**
```
Mapping Rule: SUBSTRING
Generated: SUBSTRING(src.[Description], 1, 500)
```

**Result:** All data fits, no manual calculation needed

---

### Use Case 2: Multiple Truncations

**Problem:**
Migrating from CRM with all text fields as NVARCHAR(MAX)

**Excel:**
| New Column | New DataType | Old Column | Mapping Rule |
|-----------|--------------|------------|--------------|
| FirstName | NVARCHAR(50) | FirstName | `SUBSTRING` |
| LastName | NVARCHAR(50) | LastName | `SUBSTRING` |
| Email | NVARCHAR(100) | EmailAddress | `SUBSTRING` |
| Company | NVARCHAR(200) | CompanyName | `SUBSTRING` |
| Notes | NVARCHAR(1000) | Comments | `SUBSTRING` |

**Generated SQL:**
```sql
SELECT
    SUBSTRING(src.[FirstName], 1, 50) AS [FirstName],
    SUBSTRING(src.[LastName], 1, 50) AS [LastName],
    SUBSTRING(src.[EmailAddress], 1, 100) AS [Email],
    SUBSTRING(src.[CompanyName], 1, 200) AS [Company],
    SUBSTRING(src.[Comments], 1, 1000) AS [Notes]
FROM [OldCRM].[dbo].[Contacts] AS src;
```

---

### Use Case 3: Schema Evolution

**Scenario:** Destination schema may change during development

**Excel (Initial):**
```
New Column: ProductName
New DataType: VARCHAR(100)
Mapping Rule: SUBSTRING
```

**Later, schema updated:**
```
New Column: ProductName
New DataType: VARCHAR(150)  ← Changed
Mapping Rule: SUBSTRING      ← No change needed!
```

**Result:**
- Generated SQL automatically uses 150
- No need to update mapping rules
- One place to maintain (data type column)

---

### Use Case 4: Data Quality - Prevent INSERT Failures

**Problem:**
```sql
-- This fails if source > 100 chars
INSERT INTO Products (Name)
SELECT Name FROM OldProducts
WHERE LEN(Name) > 100;  -- Error!
```

**Solution with Auto-SUBSTRING:**
```
Mapping Rule: SUBSTRING

Generated:
INSERT INTO Products (Name)
SELECT SUBSTRING(Name, 1, 100) FROM OldProducts;  -- Always works!
```

## Limitations

### 1. Always Starts at Position 1
Auto-length always uses start position 1.

For custom start positions, use explicit:
```
SUBSTRING(Column, 5, length)  ← Use explicit syntax
```

### 2. MAX Data Type
For VARCHAR(MAX) or NVARCHAR(MAX):
- Falls back to 255 characters
- Adds warning comment
- May need manual adjustment

### 3. Non-String Types
For INT, DATE, etc.:
- Falls back to 255 characters
- Likely not what you want
- Use explicit substring or different rule

### 4. Unicode Considerations
```
NVARCHAR(50) can store 50 characters (any Unicode)
VARCHAR(50) can store 50 bytes (may be < 50 chars for Unicode)
```

Auto-length uses character count, not byte count.

## Migration Guide

### Updating Existing Templates

**Before (Manual Length):**
| Mapping Rule |
|--------------|
| `LEFT(Description, 100)` |
| `LEFT(Title, 50)` |
| `LEFT(Name, 200)` |

**After (Auto Length):**
| Mapping Rule |
|--------------|
| `SUBSTRING` |
| `SUBSTRING` |
| `SUBSTRING` |

**Benefits:**
- Fewer columns to maintain
- Adapts to schema changes
- Less error-prone

### When NOT to Change

Keep explicit length when:
- Length has business meaning
- Extracting specific positions
- Length independent of destination

**Examples:**
```
Keep: LEFT(Phone, 3)           ← Area code is always 3
Keep: RIGHT(SSN, 4)            ← Last 4 is business rule
Keep: SUBSTRING(Code, 5, 3)    ← Position matters
Change: LEFT(Description, 100) → SUBSTRING  ← Just truncating
```

## Advanced Examples

### Example 1: Combined with Type Conversion

**Excel:**
| New Column | New DataType | Old Column | Mapping Rule |
|-----------|--------------|------------|--------------|
| ShortCode | VARCHAR(20) | CodeField | `SUBSTRING` |
| CodeAsInt | INT | CodeField | `CAST(SUBSTRING(CodeField, 1, 10) AS INT)` |

**Note:** For CAST, use explicit SUBSTRING or ExpressionMappingRule

---

### Example 2: With NULL Handling

**Excel:**
| Mapping Rule |
|--------------|
| `COALESCE(SUBSTRING, 'N/A')` |

**Issue:** Won't work - SUBSTRING is keyword, not expression

**Solution:** Use explicit or expression rule:
```
COALESCE(LEFT(Column, 100), 'N/A')
```

---

### Example 3: Conditional Truncation

**Want:** Only truncate if length > max

**Excel:**
```
Mapping Rule: IF LEN(Description) > 100 THEN SUBSTRING(Description, 1, 100) ELSE Description
```

**Better:** Just use `SUBSTRING` - it handles short strings gracefully
```
SUBSTRING('short', 1, 100) → 'short' (doesn't pad)
```

## Error Prevention

### Common Errors Prevented

#### Error 1: String Truncation Violation
```
Source: Description VARCHAR(5000)
Dest:   Notes VARCHAR(500)

Without SUBSTRING:
INSERT INTO Products (Notes)
SELECT Description FROM OldProducts;
ERROR: String or binary data would be truncated

With AUTO-SUBSTRING:
INSERT INTO Products (Notes)  
SELECT SUBSTRING(Description, 1, 500) FROM OldProducts;
SUCCESS: Data fits perfectly
```

#### Error 2: Hardcoded Length Mismatch
```
Dest Column:  VARCHAR(100)
Mapping Rule: LEFT(Column, 150)  ← Longer than dest!

Better:
Mapping Rule: SUBSTRING  ← Auto-uses 100
```

#### Error 3: Schema Change Breaks Migration
```
Old Schema: Name VARCHAR(100)
Rule: LEFT(Name, 100)
SQL: LEFT(src.[Name], 100)  ← Works

New Schema: Name VARCHAR(150)  ← Changed
Rule: LEFT(Name, 100)           ← Unchanged
SQL: LEFT(src.[Name], 100)     ← Still 100, not 150!

With AUTO-SUBSTRING:
Rule: SUBSTRING                 ← No change needed
SQL: SUBSTRING(src.[Name], 1, 150)  ← Auto-adapts!
```

## Best Practices Summary

✅ **DO** use `SUBSTRING` for:
- Truncating to fit destination
- Migrating oversized columns
- Reducing text field sizes
- Ensuring INSERT success

❌ **DON'T** use `SUBSTRING` for:
- Extracting specific components (use LEFT/RIGHT/explicit SUBSTRING)
- Business-rule-based lengths (use explicit)
- Complex position calculations (use explicit)

## Performance Considerations

### Runtime Performance
```
SUBSTRING(column, 1, N) is very fast
- O(N) complexity
- No table scans
- No index usage
- Minimal overhead
```

### Storage Impact
```
Before: Description VARCHAR(5000)  (5000 bytes per row)
After:  Notes VARCHAR(500)         (500 bytes per row)
Savings: 4500 bytes per row = 90% reduction
```

For 1 million rows: ~4.5 GB saved!

## Related Features

- **Insert Order**: Organize column sequence
- **Database Name Support**: Three-part naming
- **CASE WHEN Lookups**: Value mapping
- **Type Conversion**: Convert after substring

## Conclusion

The auto-length SUBSTRING feature simplifies data migrations by automatically adapting to destination column sizes, reducing maintenance burden and preventing common truncation errors. It's perfect for scenarios where you need to fit data into smaller columns without worrying about exact lengths.
