# Complete Insert Order Example - E-Commerce Migration

## Scenario
Migrating an e-commerce system with the following table dependencies:

```
LookupTypes → Categories → Products → Orders → OrderDetails
     ↓
  Countries → Customers → Orders
     ↓
   States
```

## Excel Mapping Template

### Step 1: Assign Table-Level Orders

| Table | Minimum Order | Range | Purpose |
|-------|--------------|-------|---------|
| **LookupTypes** | 10 | 10-19 | Referenced by Categories and other lookups |
| **Countries** | 20 | 20-29 | Referenced by States and Customers |
| **States** | 30 | 30-39 | Referenced by Customers |
| **Categories** | 100 | 100-109 | Referenced by Products |
| **Customers** | 110 | 110-119 | Referenced by Orders |
| **Products** | 120 | 120-129 | Referenced by Orders and OrderDetails |
| **Orders** | 200 | 200-209 | Referenced by OrderDetails |
| **OrderDetails** | 210 | 210-219 | Leaf table (no dependents) |

### Step 2: Complete Mapping

| New Table | New Column | New DataType | ... | Insert Order | Old Table | Old Column | Notes |
|-----------|-----------|--------------|-----|--------------|-----------|-----------|-------|
| **LookupTypes** | TypeId | INT | ... | **10** | dbo.OldLookups | LookupTypeId | PK - First |
| LookupTypes | TypeName | NVARCHAR(50) | ... | 11 | dbo.OldLookups | TypeName | |
| LookupTypes | Description | NVARCHAR(200) | ... | 12 | dbo.OldLookups | Desc | |
| **Countries** | CountryId | INT | ... | **20** | dbo.OldCountries | Id | PK - First |
| Countries | CountryName | NVARCHAR(100) | ... | 21 | dbo.OldCountries | Name | |
| Countries | CountryCode | CHAR(2) | ... | 22 | dbo.OldCountries | Code | |
| **States** | StateId | INT | ... | **30** | dbo.OldStates | Id | PK - First |
| States | StateName | NVARCHAR(100) | ... | 31 | dbo.OldStates | Name | |
| States | StateCode | CHAR(2) | ... | 32 | dbo.OldStates | Code | |
| States | CountryId | INT | ... | 33 | dbo.OldStates | CountryId | FK to Countries |
| **Categories** | CategoryId | INT | ... | **100** | dbo.OldCategories | CatId | PK - First |
| Categories | CategoryName | NVARCHAR(100) | ... | 101 | dbo.OldCategories | CatName | |
| Categories | TypeId | INT | ... | 102 | dbo.OldCategories | TypeId | FK to LookupTypes |
| Categories | ParentCategoryId | INT | ... | 103 | dbo.OldCategories | ParentId | Self-referencing FK |
| **Customers** | CustomerId | INT | ... | **110** | dbo.OldCustomers | CustId | PK - First |
| Customers | FirstName | NVARCHAR(50) | ... | 111 | dbo.OldCustomers | FName | |
| Customers | LastName | NVARCHAR(50) | ... | 112 | dbo.OldCustomers | LName | |
| Customers | Email | NVARCHAR(100) | ... | 113 | dbo.OldCustomers | EmailAddr | |
| Customers | CountryId | INT | ... | 114 | dbo.OldCustomers | Country | FK to Countries |
| Customers | StateId | INT | ... | 115 | dbo.OldCustomers | State | FK to States |
| Customers | FullName | NVARCHAR(101) | ... | 116 | | | Computed: FirstName + LastName |
| **Products** | ProductId | INT | ... | **120** | dbo.OldProducts | ProdId | PK - First |
| Products | ProductName | NVARCHAR(200) | ... | 121 | dbo.OldProducts | ProdName | |
| Products | CategoryId | INT | ... | 122 | dbo.OldProducts | CatId | FK to Categories |
| Products | Price | DECIMAL(10,2) | ... | 123 | dbo.OldProducts | UnitPrice | |
| Products | SKU | NVARCHAR(50) | ... | 124 | dbo.OldProducts | SKU | |
| **Orders** | OrderId | INT | ... | **200** | dbo.OldOrders | OrderId | PK - First |
| Orders | CustomerId | INT | ... | 201 | dbo.OldOrders | CustId | FK to Customers |
| Orders | OrderDate | DATETIME | ... | 202 | dbo.OldOrders | OrderDt | |
| Orders | TotalAmount | DECIMAL(10,2) | ... | 203 | dbo.OldOrders | Total | |
| **OrderDetails** | DetailId | INT | ... | **210** | dbo.OldOrderItems | ItemId | PK - First |
| OrderDetails | OrderId | INT | ... | 211 | dbo.OldOrderItems | OrderId | FK to Orders |
| OrderDetails | ProductId | INT | ... | 212 | dbo.OldOrderItems | ProdId | FK to Products |
| OrderDetails | Quantity | INT | ... | 213 | dbo.OldOrderItems | Qty | |
| OrderDetails | UnitPrice | DECIMAL(10,2) | ... | 214 | dbo.OldOrderItems | Price | |
| OrderDetails | LineTotal | DECIMAL(10,2) | ... | 215 | | | Computed: Quantity * UnitPrice |

## Generated SQL Output

```sql
-- =====================================================
-- Advanced Data Migration SQL
-- Generated: 2024-01-15 14:30:00
-- Source Database: OldECommerceDB
-- Destination Database: NewECommerceDB
-- Total Tables: 8
-- Total Columns: 38
-- =====================================================

-- Tables are ordered by their minimum Insert Order:
--   LookupTypes (Order: 10)
--   Countries (Order: 20)
--   States (Order: 30)
--   Categories (Order: 100)
--   Customers (Order: 110)
--   Products (Order: 120)
--   Orders (Order: 200)
--   OrderDetails (Order: 210)

BEGIN TRANSACTION;

-- ============================================
-- Table: LookupTypes
-- Source Tables: dbo.OldLookups
-- Columns: 3
-- Ordered by: Insert Order
-- ============================================

INSERT INTO [NewECommerceDB].[dbo].[LookupTypes] (
    [TypeId],        -- Order 10: PK
    [TypeName],      -- Order 11
    [Description]    -- Order 12
)
SELECT
    src.[LookupTypeId] AS [TypeId],
    src.[TypeName] AS [TypeName],
    src.[Desc] AS [Description]
FROM [OldECommerceDB].[dbo].[OldLookups] AS src
WHERE NOT EXISTS (
    SELECT 1 FROM [NewECommerceDB].[dbo].[LookupTypes] dest
    WHERE dest.[TypeId] = src.[LookupTypeId]
);

-- Records inserted: @@ROWCOUNT

-- ============================================
-- Table: Countries
-- Source Tables: dbo.OldCountries
-- Columns: 3
-- Ordered by: Insert Order
-- ============================================

INSERT INTO [NewECommerceDB].[dbo].[Countries] (
    [CountryId],     -- Order 20: PK
    [CountryName],   -- Order 21
    [CountryCode]    -- Order 22
)
SELECT
    src.[Id] AS [CountryId],
    src.[Name] AS [CountryName],
    src.[Code] AS [CountryCode]
FROM [OldECommerceDB].[dbo].[OldCountries] AS src
WHERE NOT EXISTS (
    SELECT 1 FROM [NewECommerceDB].[dbo].[Countries] dest
    WHERE dest.[CountryId] = src.[Id]
);

-- Records inserted: @@ROWCOUNT

-- ============================================
-- Table: States
-- Source Tables: dbo.OldStates
-- Columns: 4
-- Ordered by: Insert Order
-- ============================================

INSERT INTO [NewECommerceDB].[dbo].[States] (
    [StateId],       -- Order 30: PK
    [StateName],     -- Order 31
    [StateCode],     -- Order 32
    [CountryId]      -- Order 33: FK to Countries (already inserted ✓)
)
SELECT
    src.[Id] AS [StateId],
    src.[Name] AS [StateName],
    src.[Code] AS [StateCode],
    src.[CountryId] AS [CountryId]
FROM [OldECommerceDB].[dbo].[OldStates] AS src
WHERE NOT EXISTS (
    SELECT 1 FROM [NewECommerceDB].[dbo].[States] dest
    WHERE dest.[StateId] = src.[Id]
);

-- Records inserted: @@ROWCOUNT

-- ============================================
-- Table: Categories
-- Source Tables: dbo.OldCategories
-- Columns: 4
-- Ordered by: Insert Order
-- ============================================

INSERT INTO [NewECommerceDB].[dbo].[Categories] (
    [CategoryId],        -- Order 100: PK
    [CategoryName],      -- Order 101
    [TypeId],            -- Order 102: FK to LookupTypes (already inserted ✓)
    [ParentCategoryId]   -- Order 103: Self-referencing FK
)
SELECT
    src.[CatId] AS [CategoryId],
    src.[CatName] AS [CategoryName],
    src.[TypeId] AS [TypeId],
    src.[ParentId] AS [ParentCategoryId]
FROM [OldECommerceDB].[dbo].[OldCategories] AS src
WHERE NOT EXISTS (
    SELECT 1 FROM [NewECommerceDB].[dbo].[Categories] dest
    WHERE dest.[CategoryId] = src.[CatId]
);

-- Records inserted: @@ROWCOUNT

-- ============================================
-- Table: Customers
-- Source Tables: dbo.OldCustomers
-- Columns: 7
-- Ordered by: Insert Order
-- ============================================

INSERT INTO [NewECommerceDB].[dbo].[Customers] (
    [CustomerId],    -- Order 110: PK
    [FirstName],     -- Order 111
    [LastName],      -- Order 112
    [Email],         -- Order 113
    [CountryId],     -- Order 114: FK to Countries (already inserted ✓)
    [StateId],       -- Order 115: FK to States (already inserted ✓)
    [FullName]       -- Order 116: Computed column
)
SELECT
    src.[CustId] AS [CustomerId],
    src.[FName] AS [FirstName],
    src.[LName] AS [LastName],
    src.[EmailAddr] AS [Email],
    src.[Country] AS [CountryId],
    src.[State] AS [StateId],
    CONCAT(src.[FName], ' ', src.[LName]) AS [FullName]
FROM [OldECommerceDB].[dbo].[OldCustomers] AS src
WHERE NOT EXISTS (
    SELECT 1 FROM [NewECommerceDB].[dbo].[Customers] dest
    WHERE dest.[CustomerId] = src.[CustId]
);

-- Records inserted: @@ROWCOUNT

-- ============================================
-- Table: Products
-- Source Tables: dbo.OldProducts
-- Columns: 5
-- Ordered by: Insert Order
-- ============================================

INSERT INTO [NewECommerceDB].[dbo].[Products] (
    [ProductId],     -- Order 120: PK
    [ProductName],   -- Order 121
    [CategoryId],    -- Order 122: FK to Categories (already inserted ✓)
    [Price],         -- Order 123
    [SKU]            -- Order 124
)
SELECT
    src.[ProdId] AS [ProductId],
    src.[ProdName] AS [ProductName],
    src.[CatId] AS [CategoryId],
    src.[UnitPrice] AS [Price],
    src.[SKU] AS [SKU]
FROM [OldECommerceDB].[dbo].[OldProducts] AS src
WHERE NOT EXISTS (
    SELECT 1 FROM [NewECommerceDB].[dbo].[Products] dest
    WHERE dest.[ProductId] = src.[ProdId]
);

-- Records inserted: @@ROWCOUNT

-- ============================================
-- Table: Orders
-- Source Tables: dbo.OldOrders
-- Columns: 4
-- Ordered by: Insert Order
-- ============================================

INSERT INTO [NewECommerceDB].[dbo].[Orders] (
    [OrderId],       -- Order 200: PK
    [CustomerId],    -- Order 201: FK to Customers (already inserted ✓)
    [OrderDate],     -- Order 202
    [TotalAmount]    -- Order 203
)
SELECT
    src.[OrderId] AS [OrderId],
    src.[CustId] AS [CustomerId],
    src.[OrderDt] AS [OrderDate],
    src.[Total] AS [TotalAmount]
FROM [OldECommerceDB].[dbo].[OldOrders] AS src
WHERE NOT EXISTS (
    SELECT 1 FROM [NewECommerceDB].[dbo].[Orders] dest
    WHERE dest.[OrderId] = src.[OrderId]
);

-- Records inserted: @@ROWCOUNT

-- ============================================
-- Table: OrderDetails
-- Source Tables: dbo.OldOrderItems
-- Columns: 6
-- Ordered by: Insert Order
-- ============================================

INSERT INTO [NewECommerceDB].[dbo].[OrderDetails] (
    [DetailId],      -- Order 210: PK
    [OrderId],       -- Order 211: FK to Orders (already inserted ✓)
    [ProductId],     -- Order 212: FK to Products (already inserted ✓)
    [Quantity],      -- Order 213
    [UnitPrice],     -- Order 214
    [LineTotal]      -- Order 215: Computed column
)
SELECT
    src.[ItemId] AS [DetailId],
    src.[OrderId] AS [OrderId],
    src.[ProdId] AS [ProductId],
    src.[Qty] AS [Quantity],
    src.[Price] AS [UnitPrice],
    (src.[Qty] * src.[Price]) AS [LineTotal]
FROM [OldECommerceDB].[dbo].[OldOrderItems] AS src
WHERE NOT EXISTS (
    SELECT 1 FROM [NewECommerceDB].[dbo].[OrderDetails] dest
    WHERE dest.[DetailId] = src.[ItemId]
);

-- Records inserted: @@ROWCOUNT

-- Review the above statements before committing
-- COMMIT TRANSACTION;
-- ROLLBACK TRANSACTION; -- Uncomment to undo changes

-- =====================================================
-- End of Migration SQL
-- =====================================================
```

## Key Benefits Demonstrated

### 1. **Correct Dependency Order**
- ✅ LookupTypes inserted before Categories
- ✅ Countries inserted before States
- ✅ States inserted before Customers
- ✅ Categories inserted before Products
- ✅ Customers and Products inserted before Orders
- ✅ Orders inserted before OrderDetails

### 2. **Clear Documentation**
- Table order shown in header comment
- Each table shows its order number
- FK comments indicate parent tables are already inserted

### 3. **Error Prevention**
Without Insert Order, alphabetical ordering would fail:
```
Categories → References LookupTypes (not yet inserted) ❌
Customers → References States (not yet inserted) ❌
OrderDetails → References Orders (not yet inserted) ❌
```

### 4. **Maintainability**
- Gap-based numbering (10, 20, 30, 100, 110, etc.)
- Easy to add new tables (e.g., order 105 for Brands)
- Logical grouping by function (1-99 lookups, 100-199 master data, etc.)

## Implementation Checklist

- [x] Analyze table dependencies
- [x] Assign table-level order ranges
- [x] Map all columns with appropriate orders
- [x] Verify PK columns have lowest order in each table
- [x] Verify FK columns reference already-inserted tables
- [x] Test generated SQL execution order
- [x] Document ordering scheme for team
