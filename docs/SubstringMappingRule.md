# Substring Mapping Rule

## Overview
The `SubstringMappingRule` handles text extraction and substring operations in data mappings. It supports multiple substring patterns and automatically converts them to SQL Server-compatible expressions.

## Supported Patterns

### 0. Auto-Length SUBSTRING ⭐ NEW
**Format:** `SUBSTRING` (keyword only, no parameters)

**Excel Mapping Rule:**
```
SUBSTRING
```

**Behavior:**
- Automatically uses destination column's max length
- Starts from position 1
- Uses source column from Old Column field

**Example:**

| New Column | New DataType | Old Column | Mapping Rule | Generated SQL |
|-----------|--------------|------------|--------------|---------------|
| ProductCode | VARCHAR(10) | FullProductCode | `SUBSTRING` | `SUBSTRING(src.[FullProductCode], 1, 10)` |
| ShortName | NVARCHAR(50) | LongName | `SUBSTRING` | `SUBSTRING(src.[LongName], 1, 50)` |
| Prefix | CHAR(5) | Code | `SUBSTRING` | `SUBSTRING(src.[Code], 1, 5)` |

**When to Use:**
- Truncating long strings to fit destination column
- Ensuring data doesn't exceed max length
- Quick substring without manual length calculation

**Edge Cases:**
```
DataType: VARCHAR(MAX)  → Uses LEFT(column, 255) with warning
DataType: INT           → Uses LEFT(column, 255) with warning
No DataType specified   → Uses LEFT(column, 255) with warning
```

---

### 1. SUBSTRING (SQL Server Standard)
**Format:** `SUBSTRING(columnName, startPosition, length)`

**Excel Mapping Rule:**
```
SUBSTRING(PhoneNumber, 1, 3)
```

**Generated SQL:**
```sql
SUBSTRING(src.[PhoneNumber], 1, 3)
```

**Example Use Case:** Extract area code from phone number

---

### 2. LEFT (First N Characters)
**Format:** `LEFT(columnName, length)`

**Excel Mapping Rule:**
```
LEFT(ProductCode, 3)
```

**Generated SQL:**
```sql
LEFT(src.[ProductCode], 3)
```

**Example Use Case:** Extract product category prefix

---

### 3. RIGHT (Last N Characters)
**Format:** `RIGHT(columnName, length)`

**Excel Mapping Rule:**
```
RIGHT(AccountNumber, 4)
```

**Generated SQL:**
```sql
RIGHT(src.[AccountNumber], 4)
```

**Example Use Case:** Extract last 4 digits of account number

---

### 4. MID (Excel/VBA Style)
**Format:** `MID(columnName, startPosition, length)`

**Excel Mapping Rule:**
```
MID(SSN, 4, 2)
```

**Generated SQL:**
```sql
SUBSTRING(src.[SSN], 4, 2)
```

**Example Use Case:** Extract middle digits from SSN

---

### 5. SUBSTR (Oracle/MySQL Style)
**Format:** `SUBSTR(columnName, startPosition, length)`

**Excel Mapping Rule:**
```
SUBSTR(ZipCode, 1, 5)
```

**Generated SQL:**
```sql
SUBSTRING(src.[ZipCode], 1, 5)
```

**Example Use Case:** Extract 5-digit ZIP from ZIP+4

---

### 6. Complex Patterns with CHARINDEX
**Format:** `SUBSTRING(column, CHARINDEX(delimiter, column), length)`

**Excel Mapping Rule:**
```
SUBSTRING(EmailAddress, CHARINDEX('@', EmailAddress) + 1, LEN(EmailAddress))
```

**Generated SQL:**
```sql
SUBSTRING(src.[EmailAddress], CHARINDEX('@', src.[EmailAddress]) + 1, LEN(src.[EmailAddress]))
```

**Example Use Case:** Extract domain from email address

---

## Usage in Excel Mapping Template

| New Table | New Column | New DataType | ... | Old Column | Mapping Rule | Notes |
|-----------|-----------|--------------|-----|------------|--------------|-------|
| Customers | AreaCode | VARCHAR(3) | ... | PhoneNumber | `LEFT(PhoneNumber, 3)` | Extract area code |
| Customers | PhoneLast4 | VARCHAR(4) | ... | PhoneNumber | `RIGHT(PhoneNumber, 4)` | Last 4 digits |
| Products | CategoryCode | VARCHAR(3) | ... | ProductSKU | `SUBSTRING(ProductSKU, 1, 3)` | First 3 chars |
| Products | ShortName | NVARCHAR(50) | ... | LongProductName | `SUBSTRING` | ⭐ Auto-truncate to 50 chars |
| Employees | MiddleInitial | CHAR(1) | ... | FullName | `MID(FullName, 5, 1)` | Extract initial |
| Addresses | ZipCode5 | VARCHAR(5) | ... | ZipCode | `LEFT(ZipCode, 5)` | 5-digit ZIP |
| Addresses | ShortZip | VARCHAR(5) | ... | FullZipCode | `SUBSTRING` | ⭐ Auto-truncate to 5 chars |

## Generated SQL Examples

### Example 0: Auto-Length Substring ⭐ NEW

**Excel Mapping:**
| New Table | New Column | New DataType | Old Table | Old Column | Mapping Rule |
|-----------|-----------|--------------|-----------|------------|--------------|
| Products | ShortName | NVARCHAR(50) | OldProducts | ProductName | `SUBSTRING` |
| Products | CodePrefix | VARCHAR(10) | OldProducts | ProductCode | `SUBSTRING` |
| Customers | ShortAddress | VARCHAR(100) | OldCustomers | FullAddress | `SUBSTRING` |

**Generated SQL:**
```sql
INSERT INTO [dbo].[Products] (
    [ProductId],
    [ShortName],
    [CodePrefix]
)
SELECT
    src.[Id] AS [ProductId],
    SUBSTRING(src.[ProductName], 1, 50) AS [ShortName],      -- Auto: 50 from NVARCHAR(50)
    SUBSTRING(src.[ProductCode], 1, 10) AS [CodePrefix]      -- Auto: 10 from VARCHAR(10)
FROM [dbo].[OldProducts] AS src;

INSERT INTO [dbo].[Customers] (
    [CustomerId],
    [ShortAddress]
)
SELECT
    src.[Id] AS [CustomerId],
    SUBSTRING(src.[FullAddress], 1, 100) AS [ShortAddress]   -- Auto: 100 from VARCHAR(100)
FROM [dbo].[OldCustomers] AS src;
```

**Benefits:**
- ✅ No manual length calculation
- ✅ Guaranteed to fit destination column
- ✅ Prevents truncation errors
- ✅ Easy to maintain (change data type = auto-updates length)

---

### Example 1: Extract Phone Components

**Excel Mapping:**
| New Table | New Column | Mapping Rule | Old Table | Old Column |
|-----------|-----------|--------------|-----------|------------|
| Customers | AreaCode | `LEFT(Phone, 3)` | OldCustomers | PhoneNumber |
| Customers | Exchange | `SUBSTRING(Phone, 4, 3)` | OldCustomers | PhoneNumber |
| Customers | LineNumber | `RIGHT(Phone, 4)` | OldCustomers | PhoneNumber |

**Generated SQL:**
```sql
INSERT INTO [dbo].[Customers] (
    [CustomerId],
    [AreaCode],
    [Exchange],
    [LineNumber]
)
SELECT
    src.[Id] AS [CustomerId],
    LEFT(src.[Phone], 3) AS [AreaCode],
    SUBSTRING(src.[Phone], 4, 3) AS [Exchange],
    RIGHT(src.[Phone], 4) AS [LineNumber]
FROM [dbo].[OldCustomers] AS src;
```

### Example 2: Parse Email Address

**Excel Mapping:**
| New Column | Mapping Rule | Notes |
|-----------|--------------|-------|
| EmailDomain | `SUBSTRING(Email, CHARINDEX('@', Email) + 1, LEN(Email))` | Extract domain |
| EmailUser | `LEFT(Email, CHARINDEX('@', Email) - 1)` | Extract username |

**Generated SQL:**
```sql
SELECT
    SUBSTRING(src.[Email], CHARINDEX('@', src.[Email]) + 1, LEN(src.[Email])) AS [EmailDomain],
    LEFT(src.[Email], CHARINDEX('@', src.[Email]) - 1) AS [EmailUser]
FROM [dbo].[Users] AS src;
```

### Example 3: Extract SKU Components

**Excel Mapping:**
| New Column | Mapping Rule | Old Column | Notes |
|-----------|--------------|------------|-------|
| Department | `LEFT(SKU, 2)` | ProductSKU | Dept code (positions 1-2) |
| Category | `SUBSTRING(SKU, 3, 2)` | ProductSKU | Category code (positions 3-4) |
| Sequence | `RIGHT(SKU, 4)` | ProductSKU | Sequence number (last 4) |

**Input Data:**
```
ProductSKU: "EL05-9876"
```

**Generated SQL:**
```sql
SELECT
    LEFT(src.[ProductSKU], 2) AS [Department],        -- "EL"
    SUBSTRING(src.[ProductSKU], 3, 2) AS [Category],  -- "05"
    RIGHT(src.[ProductSKU], 4) AS [Sequence]          -- "9876"
FROM [dbo].[Products] AS src;
```

## Advanced Features

### 1. Automatic Table Alias
Column references are automatically prefixed with table alias:
```
Input:  LEFT(Name, 5)
Output: LEFT(src.[Name], 5)
```

### 2. SQL Keyword Protection
SQL keywords are not aliased:
```
Input:  SUBSTRING(Column, CHARINDEX(',', Column), LEN(Column))
Output: SUBSTRING(src.[Column], CHARINDEX(',', src.[Column]), LEN(src.[Column]))
         ↑ Keywords protected ↑           ↑ Column aliased ↑
```

### 3. Complex Expression Support
Nested functions and calculations:
```
Input:  SUBSTRING(Address, CHARINDEX(',', Address) + 2, CHARINDEX(',', Address, CHARINDEX(',', Address) + 1))
Output: Properly aliased with table reference
```

### 4. NULL Handling
Automatically handles NULL values:
```
SUBSTRING(NULL, 1, 5) → NULL
LEFT(NULL, 3) → NULL
RIGHT(NULL, 2) → NULL
```

## Integration with Other Rules

### Priority Order
1. ColumnToRowMappingRule
2. ~~LookupMappingRule~~ (commented out)
3. NullMappingRule
4. **SubstringMappingRule** ← NEW
5. ExpressionMappingRule
6. ConcatenationMappingRule
7. ConditionalMappingRule
8. TypeConversionMappingRule
9. DirectMappingRule

### Interaction with Other Rules

**With ConcatenationMappingRule:**
```
Mapping Rule: LEFT(FirstName, 1) + '.' + LEFT(LastName, 1)

SubstringMappingRule:   Handles LEFT functions
ConcatenationMappingRule: Handles concatenation
Result: LEFT(src.[FirstName], 1) + '.' + LEFT(src.[LastName], 1)
```

**With ConditionalMappingRule:**
```
Mapping Rule: IF LEN(Code) > 5 THEN LEFT(Code, 5) ELSE Code

SubstringMappingRule:   Handles LEFT function
ConditionalMappingRule: Handles IF/THEN/ELSE
Result: CASE WHEN LEN(src.[Code]) > 5 THEN LEFT(src.[Code], 5) ELSE src.[Code] END
```

## Common Use Cases

### 0. Auto-Truncate Long Strings ⭐ NEW
**Problem:** Source column is VARCHAR(500), destination is VARCHAR(100)

**Solution:**
```
Mapping Rule: SUBSTRING
Result: SUBSTRING(src.[LongDescription], 1, 100)
```

**Use Cases:**
- Migrating from legacy system with large text fields
- Reducing column sizes for performance
- Fitting data into constrained destination schema
- Ensuring no truncation errors during INSERT

**Example:**
```
Source: ProductDescription VARCHAR(1000) = "Very long description..."
Dest:   ShortDescription VARCHAR(100)
Rule:   SUBSTRING
Result: Automatically truncates to 100 characters
```

---

### 1. Phone Number Formatting
```
Extract area code: LEFT(Phone, 3)
Extract exchange: SUBSTRING(Phone, 4, 3)
Extract line: RIGHT(Phone, 4)
```

### 2. Code Parsing
```
Extract category: LEFT(ProductCode, 3)
Extract subcategory: SUBSTRING(ProductCode, 4, 2)
Extract sequence: RIGHT(ProductCode, 5)
```

### 3. Address Splitting
```
Extract street: SUBSTRING(Address, 1, CHARINDEX(',', Address) - 1)
Extract city: SUBSTRING(Address, CHARINDEX(',', Address) + 2, 50)
```

### 4. Date Component Extraction (from VARCHAR)
```
Extract year: LEFT(DateString, 4)
Extract month: SUBSTRING(DateString, 5, 2)
Extract day: RIGHT(DateString, 2)
```

### 5. SKU/Barcode Parsing
```
Extract manufacturer: SUBSTRING(Barcode, 1, 3)
Extract product family: SUBSTRING(Barcode, 4, 4)
Extract variant: RIGHT(Barcode, 2)
```

## Best Practices

### 0. **Use Auto-Length for Truncation** ⭐ NEW
When migrating to smaller column sizes:
```
Good:  Mapping Rule: SUBSTRING (auto-detects dest length)
Avoid: Mapping Rule: LEFT(Column, 100) (hardcoded)

Why: If dest column changes from VARCHAR(100) to VARCHAR(150), 
     auto-length adapts automatically, hardcoded value doesn't.
```

**When to Use:**
- Source > Destination length
- Need to ensure fit
- Don't care about exact position/length
- Want automatic adaptation to schema changes

**Example Excel:**
| New Column | New DataType | Old Column | Mapping Rule | Why |
|-----------|--------------|------------|--------------|-----|
| Title | VARCHAR(100) | LongTitle | `SUBSTRING` | Auto-truncate |
| Summary | NVARCHAR(500) | FullText | `SUBSTRING` | Auto-truncate |
| Code | CHAR(20) | LongCode | `SUBSTRING` | Auto-truncate |

---

### 1. **Use Appropriate Function**
```
First N chars:  LEFT(column, N)     ← Clearer than SUBSTRING
Last N chars:   RIGHT(column, N)    ← Clearer than SUBSTRING
Middle chars:   SUBSTRING(column, start, length)
```

### 2. **Consider NULL Values**
```
LEFT(NULL, 5) → NULL
```
Add COALESCE if needed:
```
COALESCE(LEFT(Phone, 3), '000')
```

### 3. **Validate String Length**
Ensure source data is long enough:
```
-- Might fail if Name is less than 5 characters
SUBSTRING(Name, 1, 5)

-- Safer:
SUBSTRING(Name, 1, CASE WHEN LEN(Name) >= 5 THEN 5 ELSE LEN(Name) END)

-- Or use LEFT/RIGHT which handle short strings
LEFT(Name, 5)  -- Safe even if Name is 3 characters
```

### 4. **Performance Considerations**
- Substring operations are fast
- CHARINDEX can be slower on large strings
- Consider indexed computed columns for frequently queried substrings

### 5. **Document Positions**
```
Good:  SUBSTRING(SSN, 1, 3)  -- First 3 digits
Bad:   SUBSTRING(SSN, 1, 3)  -- ???
```

## Testing

### Unit Tests

```csharp
[Fact]
public void CanHandle_LeftFunction_ReturnsTrue()
{
    var rule = new SubstringMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = "LEFT(Name, 5)" 
    };
    
    Assert.True(rule.CanHandle(mapping));
}

[Fact]
public void Apply_LeftFunction_GeneratesCorrectSQL()
{
    var rule = new SubstringMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = "LEFT(ProductCode, 3)",
        OldTableName = "dbo.Products",
        NewColumn = "CategoryCode"
    };
    
    var result = rule.Apply(mapping, new MappingContext());
    
    Assert.Equal("LEFT(src.[ProductCode], 3)", result.SqlExpression);
}

[Fact]
public void Apply_SubstringFunction_GeneratesCorrectSQL()
{
    var rule = new SubstringMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = "SUBSTRING(SSN, 4, 2)",
        OldTableName = "dbo.Employees",
        NewColumn = "SSNMiddle"
    };
    
    var result = rule.Apply(mapping, new MappingContext());
    
    Assert.Equal("SUBSTRING(src.[SSN], 4, 2)", result.SqlExpression);
}

[Fact]
public void Apply_RightFunction_GeneratesCorrectSQL()
{
    var rule = new SubstringMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = "RIGHT(AccountNumber, 4)",
        OldTableName = "dbo.Accounts",
        NewColumn = "Last4Digits"
    };
    
    var result = rule.Apply(mapping, new MappingContext());
    
    Assert.Equal("RIGHT(src.[AccountNumber], 4)", result.SqlExpression);
}

[Fact]
public void Apply_MidFunction_ConvertsToSubstring()
{
    var rule = new SubstringMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = "MID(Code, 3, 2)",
        OldTableName = "dbo.Items",
        NewColumn = "CodePart"
    };
    
    var result = rule.Apply(mapping, new MappingContext());
    
    Assert.Equal("SUBSTRING(src.[Code], 3, 2)", result.SqlExpression);
}

[Fact]
public void Apply_ComplexCharIndex_GeneratesCorrectSQL()
{
    var rule = new SubstringMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = "SUBSTRING(Email, CHARINDEX('@', Email) + 1, LEN(Email))",
        OldTableName = "dbo.Users",
        NewColumn = "EmailDomain"
    };
    
    var result = rule.Apply(mapping, new MappingContext());
    
    Assert.Contains("SUBSTRING(src.[Email],", result.SqlExpression);
    Assert.Contains("CHARINDEX('@', src.[Email])", result.SqlExpression);
}
```

### Integration Tests

```csharp
[Fact]
public async Task GenerateMigrationSQL_WithSubstringRule_GeneratesCorrectInsert()
{
    var config = new DataMappingConfiguration
    {
        ColumnMappings = new List<DataColumnMapping>
        {
            new()
            {
                NewTableName = "Customers",
                NewColumn = "AreaCode",
                OldTableName = "OldCustomers",
                OldColumn = "Phone",
                MappingRule = "LEFT(Phone, 3)",
                MappingStatus = "Approved"
            }
        }
    };
    
    var sql = _engine.GenerateMigrationSQL(config, null, null);
    
    Assert.Contains("LEFT(src.[Phone], 3) AS [AreaCode]", sql);
}
```

## Real-World Examples

### Example 1: Parse Phone Number (USA Format)

**Source:** `PhoneNumber = "555-123-4567"`

**Mapping:**
| New Column | Mapping Rule | Result |
|-----------|--------------|--------|
| AreaCode | `LEFT(PhoneNumber, 3)` | "555" |
| Exchange | `SUBSTRING(PhoneNumber, 5, 3)` | "123" |
| LineNumber | `RIGHT(PhoneNumber, 4)` | "4567" |

### Example 2: Parse Product SKU

**Source:** `SKU = "ELEC-TV-55-001234"`

**Mapping:**
| New Column | Mapping Rule | Result |
|-----------|--------------|--------|
| Department | `LEFT(SKU, 4)` | "ELEC" |
| Category | `SUBSTRING(SKU, 6, 2)` | "TV" |
| Size | `SUBSTRING(SKU, 9, 2)` | "55" |
| Sequence | `RIGHT(SKU, 6)` | "001234" |

### Example 3: Parse Date String

**Source:** `DateString = "20240315"` (YYYYMMDD)

**Mapping:**
| New Column | Mapping Rule | Result |
|-----------|--------------|--------|
| Year | `LEFT(DateString, 4)` | "2024" |
| Month | `SUBSTRING(DateString, 5, 2)` | "03" |
| Day | `RIGHT(DateString, 2)` | "15" |

**Or combine with conversion:**
```
Mapping Rule: CONVERT(DATE, DateString, 112)
```

### Example 4: Extract Email Domain

**Source:** `Email = "john.doe@example.com"`

**Mapping:**
| New Column | Mapping Rule | Result |
|-----------|--------------|--------|
| EmailUser | `LEFT(Email, CHARINDEX('@', Email) - 1)` | "john.doe" |
| EmailDomain | `SUBSTRING(Email, CHARINDEX('@', Email) + 1, LEN(Email))` | "example.com" |

### Example 5: Extract Initials

**Source:** `FullName = "John Michael Doe"`

**Mapping:**
| New Column | Mapping Rule |
|-----------|--------------|
| FirstInitial | `LEFT(FullName, 1)` |
| MiddleInitial | `SUBSTRING(FullName, CHARINDEX(' ', FullName) + 1, 1)` |

## Edge Cases Handled

### 1. NULL Values
```
LEFT(NULL, 5) → NULL
SUBSTRING(NULL, 1, 3) → NULL
```

### 2. Empty Strings
```
LEFT('', 5) → ''
SUBSTRING('', 1, 3) → ''
```

### 3. String Shorter Than Requested Length
```
LEFT('ABC', 5) → 'ABC' (doesn't fail)
RIGHT('ABC', 5) → 'ABC' (doesn't fail)
SUBSTRING('ABC', 1, 5) → 'ABC' (doesn't fail in SQL Server)
```

### 4. Invalid Start Position
```
SUBSTRING('ABC', 10, 3) → '' (empty string)
SUBSTRING('ABC', 0, 3) → '' (0 is invalid, returns empty)
```

### 5. Negative Values
```
RIGHT('ABC', -1) → Error
LEFT('ABC', -1) → Error
Solution: Validate inputs in Excel before generation
```

## Performance Tips

### 1. Fixed-Length Substrings are Fast
```
Fast: LEFT(SKU, 3)
Fast: RIGHT(Code, 4)
Fast: SUBSTRING(ID, 5, 2)
```

### 2. Dynamic Substrings Can Be Slower
```
Slower: SUBSTRING(Email, CHARINDEX('@', Email), ...)
Reason: CHARINDEX scans entire string
```

### 3. Consider Indexed Computed Columns
For frequently queried substrings:
```sql
ALTER TABLE Customers
ADD AreaCode AS LEFT(Phone, 3) PERSISTED;

CREATE INDEX IX_Customers_AreaCode ON Customers(AreaCode);
```

### 4. Pre-Calculate in ETL
If substring is used in JOINs or WHERE clauses, pre-calculate:
```sql
-- During migration, populate separate column
UPDATE Customers SET AreaCode = LEFT(Phone, 3);
```

## Validation Rules

### Before Generation
- Ensure source column exists
- Verify start position is valid (>= 1)
- Verify length is valid (>= 0)
- Check source data length is sufficient

### After Generation
- Test SQL with sample data
- Check for NULL handling
- Verify results match expected values
- Test edge cases (empty, short strings)

## Error Handling

### Invalid Pattern Detection
If pattern doesn't match any supported format:
```sql
-- Unsupported substring pattern: [original rule text]
```

### Warnings
Rule sets warning flag if:
- Pattern couldn't be parsed
- Expression seems incomplete
- SQL keywords not protected

## Limitations

### 1. Static Positions Only
Currently supports:
- Fixed positions: `SUBSTRING(col, 3, 2)`
- Function-based: `CHARINDEX`, `LEN`

Doesn't support:
- Variable positions from other columns
- Position calculations referencing multiple tables

### 2. Single Column Operations
Operates on one column at a time.

For multi-column operations, use ExpressionMappingRule or ConcatenationMappingRule.

### 3. SQL Server Syntax
Generated SQL is SQL Server specific.

For other databases, modify the rule.

## Related Rules

- **ConcatenationMappingRule**: Combine substring results
- **ExpressionMappingRule**: General expressions (fallback)
- **ConditionalMappingRule**: Conditional substring operations
- **TypeConversionMappingRule**: Convert substring results to other types

## Related Files

- `src\ZaDataStudio.Application\Mapping\Rules\SubstringMappingRule.cs` - Implementation
- `src\ZaDataStudio.Application\Mapping\MappingRuleEngine.cs` - Rule registration
- `src\ZaDataStudio.Application\Mapping\Rules\ConcatenationMappingRule.cs` - Related rule
- `src\ZaDataStudio.Application\Mapping\Rules\ExpressionMappingRule.cs` - Related rule

## Conclusion

The SubstringMappingRule provides powerful text extraction capabilities for data migrations, supporting multiple substring patterns and automatically converting them to SQL Server-compatible expressions with proper table aliasing and error handling.
