# Insert Order Feature - SQL Generation Organization

## Overview
Added "Insert Order" column to the Excel mapping template to control the order of both **columns** within INSERT statements AND **tables** in the migration script. This ensures that tables and columns with dependencies (like foreign keys, lookups, or computed values) are inserted in the correct order.

## Changes Made

### 1. Updated `DataColumnMapping` Entity

**File:** `src\ZaDataStudio.Domain\Entities\DataColumnMapping.cs`

**Added Property:**
```csharp
public int? InsertOrder { get; set; }
```

### 2. Updated Excel Template

**File:** `src\ZaDataStudio.Infrastructure\Excel\ExcelMappingService.cs`

**New Column Position:** Column 8 (after "New Column Description")

**Template Structure:**
1. New Table Name
2. New Column
3. New DataType
4. New Column Nullable
5. Has lookup
6. New Lookup Table
7. New Column Description
8. **Insert Order** ← NEW
9. Old System Table Name
10. Old Column
11. Old DataType
12. Old Column Nullable
13. Old Lookup Table
14. Mapping Rule
15. Notes
16. Mapping Status
17. AnalysisResult

### 3. Added Excel Parsing

**Added Method:**
```csharp
private int? ParseInsertOrder(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return null;

    return int.TryParse(value, out var order) ? order : null;
}
```

**Updated ParseDataMappings:**
- Reads column 8 as InsertOrder
- Parses as integer (nullable)
- Empty values default to null

### 4. Updated SQL Generation - Column Ordering

**File:** `src\ZaDataStudio.Application\Mapping\MappingRuleEngine.cs`

**Column Ordering Logic:**
```csharp
var approvedMappings = mappings
    .Where(m => string.IsNullOrWhiteSpace(m.MappingStatus) || 
               m.MappingStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase))
    .OrderBy(m => m.InsertOrder ?? int.MaxValue) // Order by InsertOrder, nulls last
    .ThenBy(m => m.NewColumn) // Secondary sort by column name
    .ToList();
```

### 5. Updated SQL Generation - Table Ordering ⭐ NEW

**Table Ordering Logic:**
```csharp
var orderedTableGroups = config.GroupedByTable
    .Select(tg => new
    {
        TableName = tg.Key,
        Mappings = tg.Value,
        MinInsertOrder = tg.Value
            .Where(m => m.InsertOrder.HasValue)
            .Select(m => m.InsertOrder!.Value)
            .DefaultIfEmpty(int.MaxValue)
            .Min()
    })
    .OrderBy(tg => tg.MinInsertOrder)
    .ThenBy(tg => tg.TableName)
    .ToList();
```

**Key Points:**
- Tables ordered by their **minimum** Insert Order value
- If a table has columns with orders: 10, 20, 30 → table order = 10
- Tables without Insert Order values sorted last (alphabetically)
- Header comment shows table ordering

## Benefits

### 1. **Table Dependency Management** ⭐ NEW
Ensures tables are inserted in the correct order based on foreign key dependencies:
```
Table Order 1-100:   LookupTables (referenced by other tables)
Table Order 101-200: Category (has FK to LookupTables)
Table Order 201-300: Products (has FK to Category)
Table Order 301-400: Orders (has FK to Products)
Table Order 401+:    OrderDetails (has FK to Orders and Products)
```

### 2. **Column Dependency Management**
Ensures columns are inserted in the correct order when there are dependencies:
```
Insert Order 1: EmployeeId (Primary Key)
Insert Order 2: DepartmentId (Foreign Key - depends on Department table)
Insert Order 3: ManagerId (Self-referencing FK - depends on EmployeeId)
Insert Order 4: FullName (Computed from other columns)
```

### 3. **Explicit Control**
Users can explicitly control both table and column order rather than relying on:
- Excel row order
- Alphabetical order
- Random order

### 4. **Better Readability**
SQL statements are more logical and easier to understand with proper ordering.

### 5. **Consistency**
Same order in:
- INSERT column list
- SELECT column list
- Table INSERT statements
- Analysis Excel export
- Generated documentation

## Usage Examples

### Example 1: Table Dependency Ordering ⭐ NEW

**Excel Mapping with Table Dependencies:**

| New Table | New Column | Insert Order | Notes |
|-----------|-----------|--------------|-------|
| **LookupTypes** | TypeId | **10** | Referenced by other tables |
| LookupTypes | TypeName | 11 | |
| **Categories** | CategoryId | **100** | References LookupTypes |
| Categories | CategoryName | 101 | |
| Categories | TypeId | 102 | FK to LookupTypes |
| **Products** | ProductId | **200** | References Categories |
| Products | ProductName | 201 | |
| Products | CategoryId | 202 | FK to Categories |
| **Orders** | OrderId | **300** | References Products |
| Orders | ProductId | 301 | FK to Products |

**Generated SQL with Table Ordering:**
```sql
-- =====================================================
-- Advanced Data Migration SQL
-- Generated: 2024-01-15 10:30:00
-- Total Tables: 4
-- Total Columns: 10
-- =====================================================

-- Tables are ordered by their minimum Insert Order:
--   LookupTypes (Order: 10)
--   Categories (Order: 100)
--   Products (Order: 200)
--   Orders (Order: 300)

BEGIN TRANSACTION;

-- ============================================
-- Table: LookupTypes
-- Ordered by: Insert Order
-- ============================================

INSERT INTO [dbo].[LookupTypes] (
    [TypeId],      -- Order 10
    [TypeName]     -- Order 11
)
SELECT ...;

-- ============================================
-- Table: Categories
-- Ordered by: Insert Order
-- ============================================

INSERT INTO [dbo].[Categories] (
    [CategoryId],   -- Order 100
    [CategoryName], -- Order 101
    [TypeId]        -- Order 102: FK to LookupTypes (already inserted)
)
SELECT ...;

-- ============================================
-- Table: Products
-- Ordered by: Insert Order
-- ============================================

INSERT INTO [dbo].[Products] (
    [ProductId],    -- Order 200
    [ProductName],  -- Order 201
    [CategoryId]    -- Order 202: FK to Categories (already inserted)
)
SELECT ...;

-- ============================================
-- Table: Orders
-- Ordered by: Insert Order
-- ============================================

INSERT INTO [dbo].[Orders] (
    [OrderId],      -- Order 300
    [ProductId]     -- Order 301: FK to Products (already inserted)
)
SELECT ...;

COMMIT TRANSACTION;
```

### Example 2: Basic Column Ordering

**Excel Mapping:**
| New Table | New Column | ... | Insert Order |
|-----------|-----------|-----|--------------|
| Employees | EmployeeId | ... | 1 |
| Employees | FirstName | ... | 2 |
| Employees | LastName | ... | 3 |
| Employees | Email | ... | 4 |

**Generated SQL:**
```sql
INSERT INTO [dbo].[Employees] (
    [EmployeeId],
    [FirstName],
    [LastName],
    [Email]
)
SELECT
    src.[Id] AS [EmployeeId],
    src.[FName] AS [FirstName],
    src.[LName] AS [LastName],
    src.[EmailAddress] AS [Email]
FROM [dbo].[Person] AS src;
```

### Example 2: Basic Column Ordering

**Excel Mapping:**
| New Table | New Column | ... | Insert Order |
|-----------|-----------|-----|--------------|
| Employees | EmployeeId | ... | 1 |
| Employees | FirstName | ... | 2 |
| Employees | LastName | ... | 3 |
| Employees | Email | ... | 4 |

**Generated SQL:**
```sql
INSERT INTO [dbo].[Employees] (
    [EmployeeId],
    [FirstName],
    [LastName],
    [Email]
)
SELECT
    src.[Id] AS [EmployeeId],
    src.[FName] AS [FirstName],
    src.[LName] AS [LastName],
    src.[EmailAddress] AS [Email]
FROM [dbo].[Person] AS src;
```

### Example 3: Column Dependency Ordering

**Excel Mapping:**
| New Column | Insert Order | Notes |
|------------|--------------|-------|
| OrderId | 1 | Primary key first |
| CustomerId | 2 | FK to Customers |
| ProductId | 3 | FK to Products |
| Quantity | 4 | Regular data |
| UnitPrice | 5 | Regular data |
| TotalPrice | 6 | Computed (Quantity * UnitPrice) |

### Example 4: Mixed Ordering

**Excel Mapping:**
| New Column | Insert Order | Result |
|------------|--------------|---------|
| CategoryId | 1 | ← Ordered first |
| ProductName | *(empty)* | ← Ordered last (alphabetically) |
| SKU | 2 | ← Ordered second |
| Description | *(empty)* | ← Ordered last (alphabetically) |

**Generated Order:**
1. CategoryId (Order: 1)
2. SKU (Order: 2)
3. Description (Order: null → sorted by name)
4. ProductName (Order: null → sorted by name)

### Example 5: Identity Columns

**Excel Mapping:**
| New Column | Insert Order | Mapping Rule | Notes |
|------------|--------------|--------------|-------|
| UserId | 1 | IDENTITY | Auto-generated, but list first |
| Username | 2 | ... | Required before dependent columns |
| CreatedDate | 3 | GETDATE() | Timestamp columns |

## Best Practices

### Table-Level Ordering ⭐ NEW

#### 1. **Lookup Tables First (1-99)**
Insert lookup/reference tables before tables that reference them:
```
Order 1-99:   LookupTypes, Countries, States, Categories
Order 100+:   Tables that reference lookups
```

#### 2. **Master Data Next (100-199)**
Insert master/parent tables before detail/child tables:
```
Order 100-199: Customers, Products, Employees
Order 200+:    Orders, Invoices, Transactions
```

#### 3. **Transaction Tables (200-299)**
Insert header/master records before details:
```
Order 200-249: OrderHeaders, InvoiceHeaders
Order 250-299: OrderDetails, InvoiceDetails
```

#### 4. **Use Hundreds for Table Groups**
Organize tables by functional area:
```
Order 1-99:    Lookups and Reference Data
Order 100-199: Core Business Objects
Order 200-299: Transactional Data
Order 300-399: Audit and Logging
Order 400-499: Reporting/Analytics
```

**Example Excel Layout:**
| New Table | First Column | Insert Order | Table Group |
|-----------|-------------|--------------|-------------|
| LookupValues | LookupId | **10** | Lookup Tables |
| Categories | CategoryId | **20** | Lookup Tables |
| Customers | CustomerId | **100** | Core Business |
| Products | ProductId | **110** | Core Business |
| Orders | OrderId | **200** | Transactions |
| OrderDetails | DetailId | **210** | Transactions |

### Column-Level Ordering

#### 1. **Primary Keys First**
Always use order 1 for primary key columns:
```
Insert Order 1: EmployeeId (PK)
Insert Order 2-N: Other columns
```

#### 2. **Foreign Keys Early**
Insert foreign keys before dependent columns:
```
Insert Order 1: OrderId (PK)
Insert Order 2: CustomerId (FK)
Insert Order 3: ProductId (FK)
Insert Order 4-N: Data columns
```

#### 3. **Computed Columns Last**
Columns that depend on other columns should come last:
```
Insert Order 1-5: Base columns
Insert Order 6: FullName (CONCAT(FirstName, LastName))
Insert Order 7: TotalPrice (Quantity * UnitPrice)
```

#### 4. **Use Gaps**
Leave gaps between orders for future insertions:
```
Insert Order: 10, 20, 30, 40, 50
Not: 1, 2, 3, 4, 5
```
This allows you to add new columns (e.g., order 25) without renumbering everything.

#### 5. **Null for Default Order**
Leave Insert Order empty for columns that don't have dependencies:
- They'll be sorted alphabetically at the end
- Reduces maintenance burden

## SQL Generation Impact

### Table Ordering Example ⭐ NEW

**Without Insert Order (Alphabetical):**
```sql
-- Tables in alphabetical order - WRONG!
INSERT INTO [dbo].[Categories] ...  -- Fails: FK to LookupTypes not yet inserted
INSERT INTO [dbo].[LookupTypes] ... -- Should be first!
INSERT INTO [dbo].[OrderDetails] ... -- Fails: FK to Orders not yet inserted
INSERT INTO [dbo].[Orders] ...      -- Fails: FK to Products not yet inserted
INSERT INTO [dbo].[Products] ...    -- Fails: FK to Categories not yet inserted
```

**With Insert Order (Dependency-Based):**
```sql
-- Tables are ordered by their minimum Insert Order:
--   LookupTypes (Order: 10)
--   Categories (Order: 100)
--   Products (Order: 200)
--   Orders (Order: 300)
--   OrderDetails (Order: 400)

INSERT INTO [dbo].[LookupTypes] ...  -- First: No dependencies
INSERT INTO [dbo].[Categories] ...   -- Second: FK to LookupTypes
INSERT INTO [dbo].[Products] ...     -- Third: FK to Categories
INSERT INTO [dbo].[Orders] ...       -- Fourth: FK to Products
INSERT INTO [dbo].[OrderDetails] ... -- Last: FK to Orders and Products
```

### Column Ordering Example

### Without Insert Order
```sql
-- Random/alphabetical order
INSERT INTO [dbo].[Employees] (
    [Email],           -- Alphabetically first
    [EmployeeId],      -- Should be first (PK)
    [FirstName],
    [LastName]
)
```

### With Insert Order
```sql
-- Logical order based on dependencies
INSERT INTO [dbo].[Employees] (
    [EmployeeId],      -- Order 1: PK
    [FirstName],       -- Order 2
    [LastName],        -- Order 3
    [Email]            -- Order 4
)
```

## Handling Special Cases

### 1. Duplicate Insert Orders
If two columns have the same Insert Order:
- Secondary sort by column name (alphabetical)
- No error thrown
- Predictable behavior

**Example:**
```
Insert Order 1: EmployeeId
Insert Order 2: FirstName, LastName (both have order 2)
```

Result: EmployeeId, FirstName, LastName (FirstName before LastName alphabetically)

### 2. Gaps in Ordering
```
Insert Order: 1, 5, 10, 100
```
- Perfectly valid
- Allows for future insertions
- Recommended approach

### 3. Negative Numbers
```
Insert Order: -1, 0, 1, 2
```
- Allowed
- Can be used for special pre-processing columns
- Useful for computed columns needed early

### 4. Large Numbers
```
Insert Order: 1000, 2000, 3000
```
- Allowed
- Good for grouping (1000s = PKs, 2000s = FKs, etc.)

## Migration from Old Templates

### Updating Existing Excel Files

**Option 1: Auto-Assign**
Add Insert Order column and auto-populate:
1. Primary keys: 1-10
2. Foreign keys: 11-20
3. Required columns: 21-50
4. Optional columns: 51-100
5. Computed columns: 101+

**Option 2: Leave Empty**
Add column but leave values empty:
- System will use alphabetical order
- No immediate action required
- Can be filled in gradually

**Option 3: Import Tool**
Create a tool to analyze dependencies and suggest orders:
- Detect PKs and FKs from schema
- Suggest ordering based on relationships
- Export updated Excel with orders

## Performance Impact

**Minimal:** 
- O(n log n) sorting overhead
- Only applied once during SQL generation
- Negligible for typical mapping sizes (< 1000 columns)

## Future Enhancements

### 1. Auto-Detection
Automatically suggest Insert Order based on:
- Schema metadata (PKs, FKs, constraints)
- Data type dependencies
- Lookup relationships
- NULL constraints

### 2. Validation
Validate Insert Order makes sense:
- Warn if PK is not first
- Warn if FK comes before PK
- Suggest reordering for optimal performance

### 3. Visualization
Display dependency graph showing:
- Column dependencies
- Recommended order
- Current order vs optimal order

### 4. Bulk Operations
- Auto-number all columns
- Re-order based on rules
- Shift orders (add 10 to all)

## Testing

### Unit Tests

```csharp
[Fact]
public void ParseInsertOrder_ValidNumber_ReturnsInteger()
{
    var result = service.ParseInsertOrder("42");
    Assert.Equal(42, result);
}

[Fact]
public void ParseInsertOrder_EmptyString_ReturnsNull()
{
    var result = service.ParseInsertOrder("");
    Assert.Null(result);
}

[Fact]
public void GenerateMigrationSQL_WithInsertOrder_OrdersCorrectly()
{
    var mappings = new List<DataColumnMapping>
    {
        new() { NewColumn = "C", InsertOrder = 3 },
        new() { NewColumn = "A", InsertOrder = 1 },
        new() { NewColumn = "B", InsertOrder = 2 }
    };
    
    var sql = engine.GenerateTableMigrationSQL(...);
    
    // Assert A, B, C order in SQL
    var indexA = sql.IndexOf("[A]");
    var indexB = sql.IndexOf("[B]");
    var indexC = sql.IndexOf("[C]");
    
    Assert.True(indexA < indexB && indexB < indexC);
}
```

### Integration Tests

1. Test with all columns having insert order
2. Test with some columns having insert order
3. Test with no columns having insert order
4. Test with duplicate insert orders
5. Test with gaps in ordering
6. Test with negative numbers
7. Test with very large numbers

## Related Files

- `src\ZaDataStudio.Domain\Entities\DataColumnMapping.cs` - Entity definition
- `src\ZaDataStudio.Infrastructure\Excel\ExcelMappingService.cs` - Excel handling
- `src\ZaDataStudio.Application\Mapping\MappingRuleEngine.cs` - SQL generation
- `docs\DatabaseName_Support.md` - Related database naming feature
- `docs\CaseWhen_LookupMapping.md` - Related lookup feature

## Conclusion

The Insert Order feature provides explicit control over column ordering in generated SQL, ensuring logical structure and proper dependency management. By allowing users to specify the order in Excel, we create more maintainable and understandable migration scripts while maintaining backwards compatibility for existing templates.
