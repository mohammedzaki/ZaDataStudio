# Insert Order Quick Reference Guide

## Table-Level Ordering Strategy

### Order Ranges by Purpose

| Range | Purpose | Examples |
|-------|---------|----------|
| **1-99** | System/Lookup Tables | LookupTypes, SystemSettings, ConfigValues |
| **100-199** | Reference Data | Countries, States, Categories, Tags |
| **200-299** | Master Data | Customers, Products, Employees, Vendors |
| **300-399** | Transaction Headers | Orders, Invoices, Quotes, Shipments |
| **400-499** | Transaction Details | OrderDetails, InvoiceLines, ShipmentItems |
| **500-599** | Audit/Logging | AuditLogs, ChangeHistory, ErrorLogs |
| **600-699** | Reporting/Analytics | Reports, Dashboards, Statistics |

### Common Table Patterns

#### Pattern 1: Simple Hierarchy
```
10: LookupTypes
20: Categories (FK → LookupTypes)
30: Products (FK → Categories)
```

#### Pattern 2: Multi-Level Hierarchy
```
10: Countries
20: States (FK → Countries)
30: Cities (FK → States)
40: Addresses (FK → Cities)
50: Customers (FK → Addresses)
```

#### Pattern 3: Header-Detail
```
100: OrderHeaders
110: OrderDetails (FK → OrderHeaders)
```

#### Pattern 4: Self-Referencing
```
100: Categories (ParentId NULL first)
101: Categories (ParentId = existing CategoryId)
```

## Column-Level Ordering Strategy

### Order Ranges by Column Type

| Range | Purpose | Examples |
|-------|---------|----------|
| **1-9** | Primary Keys | EmployeeId, CustomerId, OrderId |
| **10-29** | Foreign Keys | CategoryId, CustomerId, ProductId |
| **30-79** | Required Data | FirstName, LastName, Email, Price |
| **80-89** | Optional Data | MiddleName, Notes, Description |
| **90-99** | Computed/Generated | FullName, TotalPrice, CreatedDate |

### Column Ordering Template

**For Each Table:**
```
Order 1:    [TableName]Id              -- PK always first
Order 2-9:  Other identity/unique columns
Order 10-29: Foreign key columns
Order 30-49: Required VARCHAR/NVARCHAR columns
Order 50-69: Required numeric/date columns
Order 70-79: Required boolean/bit columns
Order 80-89: Optional columns (nullable)
Order 90-99: Computed/calculated columns
```

## Quick Setup Guide

### Step 1: Identify Dependencies

Create a dependency graph:
```
LookupTypes (no dependencies)
    ↓
Categories (depends on LookupTypes)
    ↓
Products (depends on Categories)
    ↓
Orders (depends on Customers, Products)
    ↓
OrderDetails (depends on Orders, Products)
```

### Step 2: Assign Table Orders

Start from leaves (no dependencies) to roots (many dependencies):
```
LookupTypes:   Order 10  (Level 0: no dependencies)
Categories:    Order 100 (Level 1: depends on LookupTypes)
Products:      Order 200 (Level 2: depends on Categories)
Orders:        Order 300 (Level 3: depends on Products, Customers)
OrderDetails:  Order 400 (Level 4: depends on Orders, Products)
```

### Step 3: Assign Column Orders Within Each Table

For each table, use the table's base order:
```
LookupTypes (base 10):
  - TypeId: 10
  - TypeName: 11
  - Description: 12

Categories (base 100):
  - CategoryId: 100
  - CategoryName: 101
  - TypeId: 102 (FK)
  - ParentCategoryId: 103 (FK)
```

### Step 4: Fill Excel Template

| New Table | New Column | Insert Order | Notes |
|-----------|-----------|--------------|-------|
| LookupTypes | TypeId | 10 | Table order: 10 |
| LookupTypes | TypeName | 11 | |
| Categories | CategoryId | 100 | Table order: 100 |
| Categories | CategoryName | 101 | |
| Categories | TypeId | 102 | FK to LookupTypes |

### Step 5: Verify Order

Run the analysis and check:
- [ ] Tables with no dependencies come first
- [ ] Foreign keys reference already-inserted tables
- [ ] Primary keys are first within each table
- [ ] Computed columns are last within each table

## Troubleshooting

### Problem: Circular Dependencies

**Example:**
```
Table A has FK to Table B
Table B has FK to Table A
```

**Solution:**
1. Make one FK nullable
2. Insert table with nullable FK first
3. Insert second table
4. UPDATE first table to populate FK

**Excel Strategy:**
```
Order 100: TableA (with FK to TableB set to NULL)
Order 200: TableB (with FK to TableA)
Order 300: UPDATE TableA SET FK = ... (custom SQL)
```

### Problem: Self-Referencing Tables

**Example:**
```
Categories has ParentCategoryId (FK to Categories.CategoryId)
```

**Solution:**
Insert rows in dependency order using multiple INSERT statements:

**Excel Strategy:**
```
Order 100: Categories WHERE ParentCategoryId IS NULL (root categories)
Order 101: Categories WHERE ParentCategoryId IN (SELECT from root)
Order 102: Categories WHERE ParentCategoryId IN (SELECT from previous)
```

Or disable FK constraint temporarily.

### Problem: Complex Dependencies

**Example:**
```
Orders depends on Customers AND Products
Products depends on Categories
Customers depends on Addresses
Addresses depends on Cities
```

**Solution:**
Create dependency levels:
```
Level 0 (Order 1-99):   Cities
Level 1 (Order 100-199): Addresses, Categories
Level 2 (Order 200-299): Customers, Products
Level 3 (Order 300-399): Orders
```

## Recommended Numbering Schemes

### Scheme 1: Simple (Small Projects)
```
Lookups:       10, 20, 30, ...
Master Data:   100, 110, 120, ...
Transactions:  200, 210, 220, ...
```

### Scheme 2: Gap-Based (Medium Projects)
```
Lookups:       10, 20, 30, 40, 50
Master Data:   100, 110, 120, 130, 140
Transactions:  200, 210, 220, 230, 240
Details:       300, 310, 320, 330, 340
```

### Scheme 3: Hundreds (Large Projects)
```
System:        100, 200, 300, ...
Reference:     1000, 1100, 1200, ...
Core Business: 2000, 2100, 2200, ...
Transactions:  3000, 3100, 3200, ...
Audit:         4000, 4100, 4200, ...
```

### Scheme 4: Semantic (Enterprise Projects)
```
10000-10999: Infrastructure (settings, configs)
11000-11999: Security (users, roles, permissions)
12000-12999: Lookups (static reference data)
20000-20999: Core entities (customers, products)
30000-30999: Business processes (orders, invoices)
40000-40999: Analytics (reports, aggregations)
```

## Excel Tips

### Tip 1: Color Code by Range
- Green (1-99): Lookup/Reference tables
- Blue (100-199): Master data tables
- Yellow (200-299): Transaction tables
- Orange (300+): Detail/child tables

### Tip 2: Use Formulas
Auto-calculate Insert Order based on row position:
```
=IF(A2<>A1, ROW()*10, B1+1)
```

### Tip 3: Conditional Formatting
Highlight rows where:
- Insert Order is missing (blank)
- Insert Order conflicts (duplicates)
- FK order < Referenced table order (dependency violation)

### Tip 4: Named Ranges
Create named ranges for each order group:
- `Lookups` = rows with order 1-99
- `MasterData` = rows with order 100-199
- `Transactions` = rows with order 200-299

## Validation Checklist

Before generating SQL, verify:

### Table Level
- [ ] No circular dependencies
- [ ] FK references point to lower-ordered tables
- [ ] Lookup tables have lowest orders
- [ ] Transaction tables have higher orders than master data
- [ ] Detail tables have higher orders than header tables

### Column Level
- [ ] Primary keys have lowest order in each table
- [ ] Foreign keys ordered before dependent data columns
- [ ] Computed columns have highest orders
- [ ] Identity columns listed first (even though auto-generated)
- [ ] Required columns ordered before optional

### SQL Level
- [ ] All tables in correct dependency order
- [ ] All columns in each INSERT match SELECT order
- [ ] No missing dependencies
- [ ] Transaction wraps all statements

## Common Mistakes to Avoid

### ❌ Mistake 1: Alphabetical Table Order
```
Categories before LookupTypes → FK violation!
```

### ❌ Mistake 2: Forgetting Self-References
```
Categories.ParentCategoryId references Categories.CategoryId
Solution: Insert root categories first
```

### ❌ Mistake 3: Tight Numbering
```
Order: 1, 2, 3, 4, 5 → No room for insertions
Better: 10, 20, 30, 40, 50
```

### ❌ Mistake 4: No Table Grouping
```
Random orders: 5, 17, 203, 34, 89
Better: 10, 20, 100, 110, 200
```

### ❌ Mistake 5: FK Before PK
```
Order 1: CustomerId (FK)
Order 2: OrderId (PK)
Better:
Order 1: OrderId (PK)
Order 2: CustomerId (FK)
```

## Summary

**Insert Order controls TWO levels:**
1. **Table Order**: Minimum InsertOrder of a table's columns determines when that table is inserted
2. **Column Order**: InsertOrder within a table determines column sequence in INSERT

**Best Practice Formula:**
```
Table Order = (Dependency Level × 100) + (Sequence Within Level × 10)

Examples:
- LookupTypes (Level 0, First):     0×100 + 1×10 = 10
- Categories (Level 1, First):      1×100 + 1×10 = 110
- Products (Level 2, First):        2×100 + 1×10 = 210
- Orders (Level 3, First):          3×100 + 1×10 = 310
- OrderDetails (Level 4, First):    4×100 + 1×10 = 410
```

This creates clean, maintainable, dependency-safe SQL migrations!
