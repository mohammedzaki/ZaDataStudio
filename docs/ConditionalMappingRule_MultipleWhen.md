# Conditional Mapping Rule - Multiple WHEN Clauses Support

## Overview
The `ConditionalMappingRule` now fully supports complex CASE WHEN statements with multiple conditions, IN clauses, and automatic table aliasing. It can handle both simple IF...THEN...ELSE patterns and complex multi-condition CASE statements.

## Supported Patterns

### 1. Simple IF...THEN...ELSE
**Format:** `IF condition THEN value1 ELSE value2`

**Excel Mapping Rule:**
```
IF Status = 'Active' THEN 1 ELSE 0
```

**Generated SQL:**
```sql
CASE WHEN src.[Status] = 'Active' THEN 1 ELSE 0 END
```

---

### 2. Multiple WHEN Clauses ⭐ NEW
**Format:** Complete CASE WHEN statement with multiple conditions

**Excel Mapping Rule:**
```
CASE
    WHEN Gender_Master_Code IN ('MALGT', 'M', 'ذكر') THEN 1
    WHEN Gender_Master_Code IN ('FMLGT', 'F') THEN 2
    WHEN Gender_Master_Code IN ('UNDEF') THEN 3
    ELSE 3
END
```

**Generated SQL:**
```sql
CASE
    WHEN src.[Gender_Master_Code] IN ('MALGT', 'M', 'ذكر') THEN 1
    WHEN src.[Gender_Master_Code] IN ('FMLGT', 'F') THEN 2
    WHEN src.[Gender_Master_Code] IN ('UNDEF') THEN 3
    ELSE 3
END
```

**Key Features:**
- ✅ Automatic table alias replacement
- ✅ Supports IN clauses with multiple values
- ✅ Handles Unicode characters (Arabic, Chinese, etc.)
- ✅ Preserves all WHEN clauses
- ✅ Maintains ELSE clause

---

### 3. Range Conditions
**Excel Mapping Rule:**
```
CASE
    WHEN Age < 18 THEN 'Minor'
    WHEN Age >= 18 AND Age < 65 THEN 'Adult'
    WHEN Age >= 65 THEN 'Senior'
    ELSE 'Unknown'
END
```

**Generated SQL:**
```sql
CASE
    WHEN src.[Age] < 18 THEN 'Minor'
    WHEN src.[Age] >= 18 AND src.[Age] < 65 THEN 'Adult'
    WHEN src.[Age] >= 65 THEN 'Senior'
    ELSE 'Unknown'
END
```

---

### 4. String Matching
**Excel Mapping Rule:**
```
CASE
    WHEN Email LIKE '%@company.com' THEN 'Internal'
    WHEN Email LIKE '%@partner.com' THEN 'Partner'
    WHEN Email IS NULL THEN 'Unknown'
    ELSE 'External'
END
```

**Generated SQL:**
```sql
CASE
    WHEN src.[Email] LIKE '%@company.com' THEN 'Internal'
    WHEN src.[Email] LIKE '%@partner.com' THEN 'Partner'
    WHEN src.[Email] IS NULL THEN 'Unknown'
    ELSE 'External'
END
```

---

## Excel Mapping Examples

### Example 1: Gender Code Mapping with Multiple Values

**Scenario:** Converting legacy gender codes to standardized values

**Excel Mapping:**
| New Table | New Column | Old Table | Old Column | Mapping Rule |
|-----------|-----------|-----------|------------|--------------|
| Users | GenderId | OldUsers | Gender_Master_Code | See below ↓ |

**Mapping Rule:**
```
CASE
    WHEN Gender_Master_Code IN ('MALGT', 'M', 'ذكر', 'Male') THEN 1
    WHEN Gender_Master_Code IN ('FMLGT', 'F', 'أنثى', 'Female') THEN 2
    WHEN Gender_Master_Code IN ('UNDEF', 'U', 'غير محدد') THEN 3
    ELSE 3
END
```

**Generated SQL:**
```sql
INSERT INTO [dbo].[Users] (
    [UserId],
    [GenderId]
)
SELECT
    src.[Id] AS [UserId],
    CASE
        WHEN src.[Gender_Master_Code] IN ('MALGT', 'M', 'ذكر', 'Male') THEN 1
        WHEN src.[Gender_Master_Code] IN ('FMLGT', 'F', 'أنثى', 'Female') THEN 2
        WHEN src.[Gender_Master_Code] IN ('UNDEF', 'U', 'غير محدد') THEN 3
        ELSE 3
    END AS [GenderId]
FROM [dbo].[OldUsers] AS src;
```

---

### Example 2: Status Code Mapping

**Excel Mapping:**
| New Column | Mapping Rule |
|-----------|--------------|
| StatusId | See below ↓ |

**Mapping Rule:**
```
CASE
    WHEN Status_Code IN ('ACT', 'ACTIVE', '1', 'نشط') THEN 1
    WHEN Status_Code IN ('INA', 'INACTIVE', '0', 'غير نشط') THEN 2
    WHEN Status_Code IN ('PND', 'PENDING', 'P', 'قيد الانتظار') THEN 3
    WHEN Status_Code IN ('DEL', 'DELETED', 'D', 'محذوف') THEN 4
    ELSE 0
END
```

**Generated SQL:**
```sql
CASE
    WHEN src.[Status_Code] IN ('ACT', 'ACTIVE', '1', 'نشط') THEN 1
    WHEN src.[Status_Code] IN ('INA', 'INACTIVE', '0', 'غير نشط') THEN 2
    WHEN src.[Status_Code] IN ('PND', 'PENDING', 'P', 'قيد الانتظار') THEN 3
    WHEN src.[Status_Code] IN ('DEL', 'DELETED', 'D', 'محذوف') THEN 4
    ELSE 0
END AS [StatusId]
```

---

### Example 3: Priority Level Calculation

**Excel Mapping:**
| New Column | Mapping Rule |
|-----------|--------------|
| PriorityLevel | See below ↓ |

**Mapping Rule:**
```
CASE
    WHEN Severity = 'Critical' AND Impact = 'High' THEN 1
    WHEN Severity = 'Critical' OR Impact = 'High' THEN 2
    WHEN Severity = 'Medium' THEN 3
    WHEN Severity = 'Low' THEN 4
    ELSE 5
END
```

**Generated SQL:**
```sql
CASE
    WHEN src.[Severity] = 'Critical' AND src.[Impact] = 'High' THEN 1
    WHEN src.[Severity] = 'Critical' OR src.[Impact] = 'High' THEN 2
    WHEN src.[Severity] = 'Medium' THEN 3
    WHEN src.[Severity] = 'Low' THEN 4
    ELSE 5
END AS [PriorityLevel]
```

---

### Example 4: Age Category

**Excel Mapping:**
| New Column | Mapping Rule |
|-----------|--------------|
| AgeCategory | See below ↓ |

**Mapping Rule:**
```
CASE
    WHEN Age < 13 THEN 'Child'
    WHEN Age >= 13 AND Age < 20 THEN 'Teen'
    WHEN Age >= 20 AND Age < 40 THEN 'Young Adult'
    WHEN Age >= 40 AND Age < 65 THEN 'Adult'
    WHEN Age >= 65 THEN 'Senior'
    ELSE 'Unknown'
END
```

**Generated SQL:**
```sql
CASE
    WHEN src.[Age] < 13 THEN 'Child'
    WHEN src.[Age] >= 13 AND src.[Age] < 20 THEN 'Teen'
    WHEN src.[Age] >= 20 AND src.[Age] < 40 THEN 'Young Adult'
    WHEN src.[Age] >= 40 AND src.[Age] < 65 THEN 'Adult'
    WHEN src.[Age] >= 65 THEN 'Senior'
    ELSE 'Unknown'
END AS [AgeCategory]
```

---

## Key Features

### 1. Automatic Table Aliasing ⭐
Column references are automatically prefixed with the appropriate table alias:

**Input:**
```
CASE
    WHEN Gender_Master_Code IN ('M', 'F') THEN 1
    ELSE 0
END
```

**Output:**
```sql
CASE
    WHEN src.[Gender_Master_Code] IN ('M', 'F') THEN 1
    ELSE 0
END
```

---

### 2. Multiple Table Reference Replacement ⭐
If you use explicit table names, they're replaced with the correct alias:

**Input:**
```
CASE
    WHEN anu.[Gender_Master_Code] IN ('M') THEN 1
    WHEN old_users.[Status] = 'Active' THEN 2
    ELSE 3
END
```

**Output:**
```sql
CASE
    WHEN src.[Gender_Master_Code] IN ('M') THEN 1
    WHEN src.[Status] = 'Active' THEN 2
    ELSE 3
END
```

---

### 3. Unicode Support ⭐
Handles Arabic, Chinese, and other Unicode characters in values:

**Input:**
```
CASE
    WHEN Gender_Master_Code IN ('ذكر', '男') THEN 1
    WHEN Gender_Master_Code IN ('أنثى', '女') THEN 2
    ELSE 3
END
```

**Output:**
```sql
CASE
    WHEN src.[Gender_Master_Code] IN ('ذكر', '男') THEN 1
    WHEN src.[Gender_Master_Code] IN ('أنثى', '女') THEN 2
    ELSE 3
END
```

---

### 4. IN Clause Support ⭐
Properly handles IN clauses with multiple values:

**Input:**
```
CASE
    WHEN Code IN ('A', 'B', 'C', 'D', 'E') THEN 'Group 1'
    WHEN Code IN ('F', 'G', 'H') THEN 'Group 2'
    ELSE 'Other'
END
```

**Output:**
```sql
CASE
    WHEN src.[Code] IN ('A', 'B', 'C', 'D', 'E') THEN 'Group 1'
    WHEN src.[Code] IN ('F', 'G', 'H') THEN 'Group 2'
    ELSE 'Other'
END
```

---

### 5. Complex Conditions
Supports AND, OR, NOT, IS NULL, LIKE, etc.:

**Input:**
```
CASE
    WHEN Status = 'Active' AND Type IN ('Premium', 'Gold') THEN 1
    WHEN Status = 'Active' OR Trial_End_Date > GETDATE() THEN 2
    WHEN Email IS NOT NULL THEN 3
    WHEN Name LIKE '%Test%' THEN 4
    ELSE 5
END
```

**Output:**
```sql
CASE
    WHEN src.[Status] = 'Active' AND src.[Type] IN ('Premium', 'Gold') THEN 1
    WHEN src.[Status] = 'Active' OR src.[Trial_End_Date] > GETDATE() THEN 2
    WHEN src.[Email] IS NOT NULL THEN 3
    WHEN src.[Name] LIKE '%Test%' THEN 4
    ELSE 5
END
```

---

## Complete Integration Example

### Scenario: Customer Type Classification

**Excel Mapping File:**

| New Table | New Column | New DataType | Old Table | Old Column | Mapping Rule |
|-----------|-----------|--------------|-----------|------------|--------------|
| Customers | CustomerId | INT | OldCustomers | Customer_ID | *(direct)* |
| Customers | CustomerName | NVARCHAR(100) | OldCustomers | Full_Name | *(direct)* |
| Customers | CustomerType | INT | OldCustomers | Type_Code | See below ↓ |
| Customers | StatusId | INT | OldCustomers | Status_Code | See below ↓ |

**CustomerType Mapping Rule:**
```
CASE
    WHEN Type_Code IN ('CORP', 'BUSINESS', 'B', 'شركة') THEN 1
    WHEN Type_Code IN ('IND', 'INDIVIDUAL', 'I', 'فرد') THEN 2
    WHEN Type_Code IN ('GOV', 'GOVERNMENT', 'G', 'حكومي') THEN 3
    WHEN Type_Code IN ('NPO', 'NONPROFIT', 'N', 'غير ربحي') THEN 4
    ELSE 0
END
```

**StatusId Mapping Rule:**
```
CASE
    WHEN Status_Code IN ('A', 'ACTIVE', '1') THEN 1
    WHEN Status_Code IN ('I', 'INACTIVE', '0') THEN 2
    WHEN Status_Code IN ('S', 'SUSPENDED', '-1') THEN 3
    ELSE 0
END
```

**Generated SQL:**
```sql
INSERT INTO [dbo].[Customers] (
    [CustomerId],
    [CustomerName],
    [CustomerType],
    [StatusId]
)
SELECT
    src.[Customer_ID] AS [CustomerId],
    src.[Full_Name] AS [CustomerName],
    CASE
        WHEN src.[Type_Code] IN ('CORP', 'BUSINESS', 'B', 'شركة') THEN 1
        WHEN src.[Type_Code] IN ('IND', 'INDIVIDUAL', 'I', 'فرد') THEN 2
        WHEN src.[Type_Code] IN ('GOV', 'GOVERNMENT', 'G', 'حكومي') THEN 3
        WHEN src.[Type_Code] IN ('NPO', 'NONPROFIT', 'N', 'غير ربحي') THEN 4
        ELSE 0
    END AS [CustomerType],
    CASE
        WHEN src.[Status_Code] IN ('A', 'ACTIVE', '1') THEN 1
        WHEN src.[Status_Code] IN ('I', 'INACTIVE', '0') THEN 2
        WHEN src.[Status_Code] IN ('S', 'SUSPENDED', '-1') THEN 3
        ELSE 0
    END AS [StatusId]
FROM [dbo].[OldCustomers] AS src;
```

---

## Best Practices

### 1. **Order Conditions by Specificity**
Most specific conditions first:
```
Good:
CASE
    WHEN Status = 'VIP_GOLD' THEN 1     ← Most specific
    WHEN Status LIKE 'VIP%' THEN 2      ← Less specific
    WHEN Status = 'MEMBER' THEN 3       ← General
    ELSE 4                               ← Fallback

Bad:
CASE
    WHEN Status LIKE 'VIP%' THEN 2      ← Catches VIP_GOLD first!
    WHEN Status = 'VIP_GOLD' THEN 1     ← Never reached
```

### 2. **Always Include ELSE**
Prevents NULL values:
```
Good:
CASE
    WHEN Code = 'A' THEN 1
    WHEN Code = 'B' THEN 2
    ELSE 0  ← Handles all other cases

Bad:
CASE
    WHEN Code = 'A' THEN 1
    WHEN Code = 'B' THEN 2
    -- No ELSE, returns NULL for 'C', 'D', etc.
```

### 3. **Use IN for Multiple Values**
More readable than multiple ORs:
```
Good:
WHEN Code IN ('A', 'B', 'C') THEN 1

Bad:
WHEN Code = 'A' OR Code = 'B' OR Code = 'C' THEN 1
```

### 4. **Group Related Values**
```
CASE
    -- Male values
    WHEN Gender IN ('M', 'MALE', 'ذكر', '男') THEN 1
    
    -- Female values
    WHEN Gender IN ('F', 'FEMALE', 'أنثى', '女') THEN 2
    
    -- Unknown/Other
    WHEN Gender IN ('U', 'UNKNOWN', 'غير محدد') THEN 3
    
    ELSE 0
END
```

### 5. **Document Legacy Codes**
Add comments in Excel Notes field:
```
Mapping Rule: [CASE statement]
Notes: Legacy codes - MALGT=Male (legacy), M=Male (new), ذكر=Male (Arabic)
```

---

## Common Use Cases

### 1. **Legacy Code Consolidation**
Multiple legacy systems with different codes:
```
CASE
    WHEN OldCode IN ('ACT', 'ACTIVE', '1', 'A', 'نشط') THEN 1
    ELSE 0
END
```

### 2. **Multi-Language Support**
Same values in different languages:
```
CASE
    WHEN Status IN ('Active', 'نشط', '活跃', 'Actif') THEN 1
    WHEN Status IN ('Inactive', 'غير نشط', '不活跃', 'Inactif') THEN 2
    ELSE 0
END
```

### 3. **Data Quality Improvement**
Standardize inconsistent data:
```
CASE
    WHEN Priority IN ('HIGH', 'H', '1', 'URGENT', 'عاجل') THEN 1
    WHEN Priority IN ('MED', 'MEDIUM', 'M', '2', 'متوسط') THEN 2
    WHEN Priority IN ('LOW', 'L', '3', 'منخفض') THEN 3
    ELSE 2  -- Default to medium
END
```

### 4. **Range-Based Categorization**
```
CASE
    WHEN Amount < 1000 THEN 'Small'
    WHEN Amount >= 1000 AND Amount < 10000 THEN 'Medium'
    WHEN Amount >= 10000 AND Amount < 100000 THEN 'Large'
    WHEN Amount >= 100000 THEN 'Enterprise'
    ELSE 'Unknown'
END
```

### 5. **Derived Business Logic**
```
CASE
    WHEN Customer_Type = 'VIP' AND Annual_Spend > 50000 THEN 'Platinum'
    WHEN Customer_Type = 'VIP' AND Annual_Spend > 10000 THEN 'Gold'
    WHEN Customer_Type = 'VIP' THEN 'Silver'
    WHEN Annual_Spend > 10000 THEN 'Premium'
    ELSE 'Standard'
END
```

---

## Testing

### Unit Tests

```csharp
[Fact]
public void CanHandle_MultipleWhenClauses_ReturnsTrue()
{
    var rule = new ConditionalMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = @"CASE
            WHEN Code IN ('A', 'B') THEN 1
            WHEN Code IN ('C', 'D') THEN 2
            ELSE 3
        END"
    };
    
    Assert.True(rule.CanHandle(mapping));
}

[Fact]
public void Apply_MultipleWhenWithIN_GeneratesCorrectSQL()
{
    var rule = new ConditionalMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = @"CASE
            WHEN Gender_Master_Code IN ('M', 'MALE') THEN 1
            WHEN Gender_Master_Code IN ('F', 'FEMALE') THEN 2
            ELSE 3
        END",
        OldTableName = "dbo.Users",
        NewColumn = "GenderId"
    };
    
    var result = rule.Apply(mapping, new MappingContext());
    
    Assert.Contains("CASE", result.SqlExpression);
    Assert.Contains("src.[Gender_Master_Code]", result.SqlExpression);
    Assert.Contains("IN ('M', 'MALE')", result.SqlExpression);
}

[Fact]
public void Apply_UnicodeValues_PreservesUnicode()
{
    var rule = new ConditionalMappingRule();
    var mapping = new DataColumnMapping 
    { 
        MappingRule = @"CASE
            WHEN Code IN ('ذكر', '男') THEN 1
            ELSE 2
        END",
        OldTableName = "dbo.Data",
        NewColumn = "Value"
    };
    
    var result = rule.Apply(mapping, new MappingContext());
    
    Assert.Contains("'ذكر'", result.SqlExpression);
    Assert.Contains("'男'", result.SqlExpression);
}
```

---

## Limitations

### 1. Nested CASE Statements
Currently doesn't support nested CASE:
```
-- Not supported:
CASE
    WHEN Type = 'A' THEN 
        CASE WHEN SubType = '1' THEN 'A1' ELSE 'A2' END
    ELSE 'Other'
END

-- Workaround: Split into multiple mappings
```

### 2. Computed Expressions in THEN
Limited support for complex expressions in THEN clause:
```
-- Simple values work:
THEN 1
THEN 'Value'

-- Complex expressions may not work:
THEN Amount * 1.1 + Tax
-- Workaround: Use ExpressionMappingRule
```

---

## Performance Considerations

### CASE Statement Performance
```
Fast: CASE with IN clause (indexed column)
Medium: CASE with = comparisons
Slow: CASE with LIKE patterns
Very Slow: CASE with complex OR conditions
```

### Optimization Tips

**1. Use IN instead of multiple ORs:**
```
Good:  WHEN Code IN ('A', 'B', 'C') THEN 1
Bad:   WHEN Code = 'A' OR Code = 'B' OR Code = 'C' THEN 1
```

**2. Order by frequency:**
```
CASE
    WHEN Status = 'Active' THEN 1   ← Most common (checked first)
    WHEN Status = 'Pending' THEN 2  ← Less common
    WHEN Status = 'Archived' THEN 3 ← Rare
    ELSE 0
END
```

**3. Consider indexed computed columns:**
```sql
ALTER TABLE Users
ADD GenderId AS (
    CASE
        WHEN Gender_Master_Code IN ('M', 'MALE') THEN 1
        WHEN Gender_Master_Code IN ('F', 'FEMALE') THEN 2
        ELSE 3
    END
) PERSISTED;

CREATE INDEX IX_Users_GenderId ON Users(GenderId);
```

---

## Related Rules

- **LookupMappingRule**: For simple value lookups
- **ExpressionMappingRule**: For complex calculations
- **TypeConversionMappingRule**: For type conversions after CASE

---

## Conclusion

The enhanced ConditionalMappingRule provides powerful multi-condition mapping capabilities with automatic table aliasing, Unicode support, and IN clause handling, making it perfect for consolidating legacy codes, supporting multi-language data, and implementing complex business logic during data migrations.
