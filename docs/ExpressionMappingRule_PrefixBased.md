# Expression Mapping Rule - Prefix-Based Conflict Resolution

## Overview
The `ExpressionMappingRule` has been updated to require an explicit prefix (`exp:` or `expression:`) to avoid conflicts with other rules like `ConcatenationMappingRule`, `ConditionalMappingRule`, and `SubstringMappingRule`.

## Problem Solved

### Before (Conflicting)
The old `ExpressionMappingRule` would trigger on any expression containing:
- Parentheses `()`
- Plus signs `+`
- Keywords like `CASE`, `CAST`, `CONCAT`, etc.

**Issue:** This caused conflicts with:
- `ConcatenationMappingRule`: `FirstName + ' ' + LastName`
- `ConditionalMappingRule`: `CASE WHEN...`
- `SubstringMappingRule`: `SUBSTRING(...)`, `LEFT(...)`, etc.

### After (Explicit Prefix)
Now requires explicit prefix:
- `exp:` or `expression:`
- No more conflicts - other rules process first
- Clear intent: "This is a custom expression"

## New Syntax

### Short Prefix: `exp:`
```
exp: Amount * 1.15
exp: CAST(OldDate AS DATE)
exp: ISNULL(Field1, 0) + ISNULL(Field2, 0)
```

### Long Prefix: `expression:`
```
expression: Amount * 1.15
expression: CAST(OldDate AS DATE)
expression: ISNULL(Field1, 0) + ISNULL(Field2, 0)
```

## Excel Mapping Examples

### Example 1: Mathematical Expression

**Excel Mapping:**
| New Column | Old Column | Mapping Rule |
|-----------|------------|--------------|
| TotalWithTax | Amount | `exp: Amount * 1.15` |

**Generated SQL:**
```sql
src.[Amount] * 1.15 AS [TotalWithTax]
```

---

### Example 2: Type Conversion

**Excel Mapping:**
| New Column | Old Column | Mapping Rule |
|-----------|------------|--------------|
| BirthDate | BirthDateString | `exp: CAST(BirthDateString AS DATE)` |

**Generated SQL:**
```sql
CAST(src.[BirthDateString] AS DATE) AS [BirthDate]
```

---

### Example 3: Complex Calculation

**Excel Mapping:**
| New Column | Old Column | Mapping Rule |
|-----------|------------|--------------|
| TotalPrice | - | `exp: Quantity * UnitPrice * (1 - DiscountPercent / 100)` |

**Generated SQL:**
```sql
src.[Quantity] * src.[UnitPrice] * (1 - src.[DiscountPercent] / 100) AS [TotalPrice]
```

---

### Example 4: NULL Handling

**Excel Mapping:**
| New Column | Old Column | Mapping Rule |
|-----------|------------|--------------|
| SafeTotal | Total | `exp: ISNULL(Total, 0)` |

**Generated SQL:**
```sql
ISNULL(src.[Total], 0) AS [SafeTotal]
```

---

### Example 5: String Manipulation

**Excel Mapping:**
| New Column | Old Column | Mapping Rule |
|-----------|------------|--------------|
| CleanPhone | Phone | `exp: REPLACE(REPLACE(Phone, '-', ''), ' ', '')` |

**Generated SQL:**
```sql
REPLACE(REPLACE(src.[Phone], '-', ''), ' ', '') AS [CleanPhone]
```

---

## Rule Priority Order (Updated)

```
1. ColumnToRowMappingRule        (Pivot operations)
2. LookupMappingRule (commented) (Lookup joins)
3. NullMappingRule               (NULL/N/A handling)
4. SubstringMappingRule          (SUBSTRING, LEFT, RIGHT)
5. ExpressionMappingRule ← YOU ARE HERE (exp: prefix required)
6. ConcatenationMappingRule      (+ operator, no prefix needed)
7. ConditionalMappingRule        (CASE WHEN, no prefix needed)
8. TypeConversionMappingRule     (Auto type conversions)
9. DirectMappingRule             (Fallback)
```

### Why This Order?
1. **Substring First**: `LEFT(Column, 5)` doesn't need `exp:` prefix
2. **Expression Next**: `exp: Amount * 1.15` requires prefix
3. **Concatenation After**: `FirstName + ' ' + LastName` no prefix needed

## Migration Guide

### Updating Existing Mappings

**Before (would cause conflicts):**
| Mapping Rule | Problem |
|--------------|---------|
| `Amount * 1.15` | ✗ Might not trigger ExpressionMappingRule |
| `CAST(Date AS DATE)` | ✗ Might not trigger ExpressionMappingRule |
| `ISNULL(Field, 0)` | ✗ Might not trigger ExpressionMappingRule |

**After (explicit and clear):**
| Mapping Rule | Solution |
|--------------|----------|
| `exp: Amount * 1.15` | ✓ Explicit expression |
| `exp: CAST(Date AS DATE)` | ✓ Explicit expression |
| `exp: ISNULL(Field, 0)` | ✓ Explicit expression |

### Other Rules Don't Need Prefix

| Rule Type | Mapping Rule | Prefix? |
|-----------|--------------|---------|
| Substring | `LEFT(Name, 5)` | ❌ No |
| Substring | `SUBSTRING(Code, 1, 3)` | ❌ No |
| Concatenation | `FirstName + ' ' + LastName` | ❌ No |
| Conditional | `CASE WHEN...THEN...END` | ❌ No |
| Expression | `exp: Amount * 1.15` | ✅ Yes |
| Expression | `expression: CAST(...)` | ✅ Yes |

## Features

### 1. Automatic Column Aliasing ⭐

**Input:**
```
exp: Amount * Quantity
```

**Output:**
```sql
src.[Amount] * src.[Quantity]
```

Columns are automatically prefixed with table alias.

---

### 2. SQL Keyword Protection ⭐

**Input:**
```
exp: CAST(Amount AS DECIMAL)
```

**Output:**
```sql
CAST(src.[Amount] AS DECIMAL)
```

`CAST`, `AS`, `DECIMAL` are not aliased.

---

### 3. Nested Function Support ⭐

**Input:**
```
exp: ROUND(Amount * 1.15, 2)
```

**Output:**
```sql
ROUND(src.[Amount] * 1.15, 2)
```

Functions can be nested.

---

### 4. Multiple Column References ⭐

**Input:**
```
exp: (Quantity * UnitPrice) - DiscountAmount
```

**Output:**
```sql
(src.[Quantity] * src.[UnitPrice]) - src.[DiscountAmount]
```

All columns get aliased.

---

## Complete Example: Order Line Total

### Excel Mapping

| New Table | New Column | Old Table | Old Column | Mapping Rule |
|-----------|-----------|-----------|------------|--------------|
| OrderLines | LineId | OldOrderItems | ItemId | *(direct)* |
| OrderLines | Quantity | OldOrderItems | Qty | *(direct)* |
| OrderLines | UnitPrice | OldOrderItems | Price | *(direct)* |
| OrderLines | DiscountPercent | OldOrderItems | Discount | *(direct)* |
| OrderLines | LineTotal | - | - | `exp: Quantity * UnitPrice * (1 - DiscountPercent / 100)` |
| OrderLines | LineTotalRounded | - | - | `exp: ROUND(Quantity * UnitPrice * (1 - DiscountPercent / 100), 2)` |

### Generated SQL

```sql
INSERT INTO [dbo].[OrderLines] (
    [LineId],
    [Quantity],
    [UnitPrice],
    [DiscountPercent],
    [LineTotal],
    [LineTotalRounded]
)
SELECT
    src.[ItemId] AS [LineId],
    src.[Qty] AS [Quantity],
    src.[Price] AS [UnitPrice],
    src.[Discount] AS [DiscountPercent],
    src.[Quantity] * src.[UnitPrice] * (1 - src.[DiscountPercent] / 100) AS [LineTotal],
    ROUND(src.[Quantity] * src.[UnitPrice] * (1 - src.[DiscountPercent] / 100), 2) AS [LineTotalRounded]
FROM [dbo].[OldOrderItems] AS src;
```

## When to Use Each Rule

### Use ExpressionMappingRule (`exp:` prefix) When:
- ✅ Complex mathematical calculations
- ✅ Type conversions (CAST, CONVERT)
- ✅ NULL handling with multiple operations
- ✅ Nested function calls
- ✅ Custom SQL expressions not covered by other rules

### Use ConcatenationMappingRule (no prefix) When:
- ✅ Simple string concatenation: `FirstName + ' ' + LastName`
- ✅ Combining fields: `City + ', ' + State`

### Use SubstringMappingRule (no prefix) When:
- ✅ Extracting parts: `LEFT(Phone, 3)`
- ✅ Truncating: `SUBSTRING(Description, 1, 100)`

### Use ConditionalMappingRule (no prefix) When:
- ✅ Conditional logic: `CASE WHEN...THEN...END`
- ✅ Value mapping: `IF...THEN...ELSE`

## Comparison Table

| Scenario | Old Rule (Conflicting) | New Rule (Prefixed) |
|----------|------------------------|---------------------|
| Math | `Amount * 1.15` | `exp: Amount * 1.15` |
| Concat | `FirstName + LastName` | `FirstName + LastName` (no change) |
| Substring | `LEFT(Name, 5)` | `LEFT(Name, 5)` (no change) |
| Conditional | `CASE WHEN...` | `CASE WHEN...` (no change) |
| Cast | `CAST(Field AS INT)` | `exp: CAST(Field AS INT)` |
| Complex | `ISNULL(A, 0) + B` | `exp: ISNULL(A, 0) + B` |

## Testing

### Unit Tests

```csharp
[Fact]
public void CanHandle_WithExpPrefix_ReturnsTrue()
{
    var rule = new ExpressionMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = "exp: Amount * 1.15" 
    };
    
    Assert.True(rule.CanHandle(mapping));
}

[Fact]
public void CanHandle_WithExpressionPrefix_ReturnsTrue()
{
    var rule = new ExpressionMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = "expression: CAST(Field AS INT)" 
    };
    
    Assert.True(rule.CanHandle(mapping));
}

[Fact]
public void CanHandle_WithoutPrefix_ReturnsFalse()
{
    var rule = new ExpressionMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = "Amount * 1.15" 
    };
    
    Assert.False(rule.CanHandle(mapping));
}

[Fact]
public void Apply_RemovesPrefixAndAliasesColumns()
{
    var rule = new ExpressionMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = "exp: Amount * Quantity",
        OldTableName = "dbo.Orders",
        NewColumn = "Total"
    };
    
    var result = rule.Apply(mapping, new MappingContext());
    
    Assert.Equal("src.[Amount] * src.[Quantity]", result.SqlExpression);
}

[Fact]
public void Apply_ProtectsSQLKeywords()
{
    var rule = new ExpressionMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = "exp: CAST(Amount AS DECIMAL)",
        OldTableName = "dbo.Orders",
        NewColumn = "DecimalAmount"
    };
    
    var result = rule.Apply(mapping, new MappingContext());
    
    Assert.Contains("CAST", result.SqlExpression);
    Assert.Contains("AS", result.SqlExpression);
    Assert.Contains("DECIMAL", result.SqlExpression);
    Assert.Contains("src.[Amount]", result.SqlExpression);
}
```

## Best Practices

### 1. Always Use Prefix
```
Good:  exp: Amount * 1.15
Bad:   Amount * 1.15  (might not work)
```

### 2. Use Short Prefix for Readability
```
Preferred:  exp: calculation
Acceptable: expression: calculation
```

### 3. Complex Expressions in Expression Rule
```
Good:  exp: ROUND((Quantity * Price) * (1 - Discount / 100), 2)
Bad:   Split across multiple mappings (harder to maintain)
```

### 4. Document Complex Expressions
```
Mapping Rule: exp: ((Qty * Price) - Discount) * (1 + TaxRate / 100)
Notes: Calculate line total with discount and tax
```

## Performance

### Expression Evaluation
- Evaluated per row during SELECT
- Can impact performance on large datasets
- Consider pre-calculating complex expressions

### Index Impact
- Expressions can't use indexes on source columns
- May prevent index seeks
- Test with representative data volumes

### Optimization Tips
```
Slow: exp: UPPER(LOWER(TRIM(Column)))
Fast: exp: Column  (if already clean)

Slow: exp: CAST(CAST(Field AS VARCHAR) AS INT)
Fast: exp: CAST(Field AS INT)
```

## Related Rules

- **SubstringMappingRule**: Text extraction (no prefix)
- **ConcatenationMappingRule**: String combination (no prefix)
- **ConditionalMappingRule**: Conditional logic (no prefix)
- **TypeConversionMappingRule**: Auto type conversion (no prefix)

## Conclusion

By requiring an explicit `exp:` or `expression:` prefix, the ExpressionMappingRule now:
- ✅ Avoids conflicts with other rules
- ✅ Makes intent clear and explicit
- ✅ Provides better control over rule selection
- ✅ Maintains all previous functionality
- ✅ Adds automatic column aliasing

Simply prefix your custom SQL expressions with `exp:` to use this rule!
