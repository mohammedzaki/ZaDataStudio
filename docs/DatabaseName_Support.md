# Database Name Support in SQL Generation

## Overview
Enhanced the `MappingRuleEngine` to support three-part naming convention `[DatabaseName].[Schema].[TableName]` in generated SQL, allowing cross-database migrations and clearer identification of source and destination databases.

## Changes Made

### 1. Updated `GenerateMigrationSQL` Method

**Added Parameters:**
- `sourceDatabase` (string, optional): Name of source database
- `destinationDatabase` (string, optional): Name of destination database

**Example:**
```csharp
var sql = _ruleEngine.GenerateMigrationSQL(
    config,
    analysisResult,
    datatypeComparisons,
    sourceDatabase: "OldSystemDB",
    destinationDatabase: "NewSystemDB",
    includeTransaction: true
);
```

### 2. Updated `GenerateTableMigrationSQL` Method

Now accepts and passes through database names to all table formatting operations.

### 3. Updated `FormatTableName` Method

**New Signature:**
```csharp
private string FormatTableName(string tableName, string databaseName = "")
```

**Logic:**
- If database name provided: `[Database].[Schema].[Table]`
- If no database name: `[Schema].[Table]`
- Defaults to `dbo` schema if not specified
- Preserves existing three-part names

### 4. Updated Helper Methods

- `GenerateLookupCaseWhen`: Accepts `sourceDatabase` parameter
- `GetSourceExpression`: Accepts `sourceDatabase` parameter

### 5. Updated `ExcelMappingService`

Added database parameters to `GenerateMigrationSQL` method to match the engine signature.

## Generated SQL Examples

### Without Database Names (Before)

```sql
INSERT INTO [dbo].[Employees] (
    [EmployeeId],
    [FullName]
)
SELECT
    src.[PersonId] AS [EmployeeId],
    src.[Name] AS [FullName]
FROM [dbo].[Person] AS src
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[Employees] dest
    WHERE dest.[EmployeeId] = src.[PersonId]
);
```

### With Database Names (After)

```sql
-- =====================================================
-- Advanced Data Migration SQL
-- Generated: 2024-01-15 10:30:00
-- Source Database: OldSystemDB
-- Destination Database: NewSystemDB
-- Total Tables: 5
-- Total Columns: 45
-- =====================================================

INSERT INTO [NewSystemDB].[dbo].[Employees] (
    [EmployeeId],
    [FullName]
)
SELECT
    src.[PersonId] AS [EmployeeId],
    src.[Name] AS [FullName]
FROM [OldSystemDB].[dbo].[Person] AS src
WHERE NOT EXISTS (
    SELECT 1 FROM [NewSystemDB].[dbo].[Employees] dest
    WHERE dest.[EmployeeId] = src.[PersonId]
);
```

## Use Cases

### 1. Cross-Database Migration

**Scenario:** Migrating data from legacy database to new database on same server

```csharp
var sql = _ruleEngine.GenerateMigrationSQL(
    config,
    analysisResult,
    datatypeComparisons,
    sourceDatabase: "Legacy_2020",
    destinationDatabase: "Modern_2024",
    includeTransaction: true
);
```

**Generated SQL:**
```sql
INSERT INTO [Modern_2024].[dbo].[Customers] (...)
SELECT ... FROM [Legacy_2020].[dbo].[Clients] ...
```

### 2. Same Database Migration

**Scenario:** Migrating within same database (different schemas)

```csharp
var sql = _ruleEngine.GenerateMigrationSQL(
    config,
    analysisResult,
    datatypeComparisons,
    sourceDatabase: "MyDatabase",
    destinationDatabase: "MyDatabase",
    includeTransaction: true
);
```

**Generated SQL:**
```sql
INSERT INTO [MyDatabase].[new_schema].[Customers] (...)
SELECT ... FROM [MyDatabase].[old_schema].[Customers] ...
```

### 3. Linked Server Migration

**Scenario:** Migrating from linked server

```csharp
var sql = _ruleEngine.GenerateMigrationSQL(
    config,
    analysisResult,
    datatypeComparisons,
    sourceDatabase: "LinkedServer.RemoteDB",
    destinationDatabase: "LocalDB",
    includeTransaction: true
);
```

**Generated SQL:**
```sql
INSERT INTO [LocalDB].[dbo].[Customers] (...)
SELECT ... FROM [LinkedServer.RemoteDB].[dbo].[Customers] ...
```

### 4. Without Database Names

**Scenario:** Standard migration (backwards compatible)

```csharp
var sql = _ruleEngine.GenerateMigrationSQL(
    config,
    analysisResult,
    datatypeComparisons,
    includeTransaction: true
);
```

**Generated SQL:**
```sql
INSERT INTO [dbo].[Customers] (...)
SELECT ... FROM [dbo].[Clients] ...
```

## Benefits

### 1. **Cross-Database Support**
- Execute migrations across different databases
- Clear identification of source and destination
- No ambiguity in table references

### 2. **Linked Server Ready**
- Supports four-part naming for linked servers
- Can include server name in database parameter
- Example: `LINKEDSERVER.DatabaseName`

### 3. **Better Documentation**
- SQL header shows source and destination databases
- Easy to understand data flow
- Self-documenting migration scripts

### 4. **Backwards Compatible**
- Database names are optional
- Existing code continues to work
- Defaults to two-part naming without databases

### 5. **Flexible**
- Works with default schemas (dbo)
- Works with custom schemas
- Handles pre-formatted table names

## FormatTableName Logic

### Input Variations

| Input | Database | Output |
|-------|----------|--------|
| `Users` | `MyDB` | `[MyDB].[dbo].[Users]` |
| `dbo.Users` | `MyDB` | `[MyDB].[dbo].[Users]` |
| `custom.Users` | `MyDB` | `[MyDB].[custom].[Users]` |
| `[MyDB].[dbo].[Users]` | `NewDB` | `[MyDB].[dbo].[Users]` (preserved) |
| `Users` | *(empty)* | `[dbo].[Users]` |
| `dbo.Users` | *(empty)* | `[dbo].[Users]` |

### Special Cases

**Already 3-part name:**
```csharp
FormatTableName("[DB].[dbo].[Table]", "NewDB")
// Returns: [DB].[dbo].[Table] (original preserved)
```

**No schema specified:**
```csharp
FormatTableName("Users", "MyDB")
// Returns: [MyDB].[dbo].[Users] (dbo added)
```

**Custom schema:**
```csharp
FormatTableName("archive.Users", "MyDB")
// Returns: [MyDB].[archive].[Users]
```

## Integration

### From Blazor UI

```csharp
// In SchemaComparison.razor.cs
private async Task GenerateMigrationSQL()
{
    var sourceDbName = ExtractDatabaseName(sourceConnectionString);
    var destDbName = ExtractDatabaseName(destConnectionString);
    
    migrationSQL = _excelMappingService.GenerateMigrationSQL(
        excelConfig,
        excelComparisonResult,
        excelComparisonResult.DatatypeComparisons,
        sourceDbName,
        destDbName
    );
}
```

### From API

```csharp
[HttpPost("generate-sql")]
public IActionResult GenerateSql(
    [FromBody] SqlGenerationRequest request)
{
    var sql = _excelMappingService.GenerateMigrationSQL(
        request.Config,
        request.AnalysisResult,
        request.DatatypeComparisons,
        request.SourceDatabase,
        request.DestinationDatabase
    );
    
    return Ok(sql);
}
```

## Testing

### Unit Tests

```csharp
[Fact]
public void FormatTableName_WithDatabase_ReturnsThreePartName()
{
    var engine = new MappingRuleEngine();
    var result = engine.FormatTableName("Users", "TestDB");
    
    Assert.Equal("[TestDB].[dbo].[Users]", result);
}

[Fact]
public void FormatTableName_WithDatabaseAndSchema_PreservesSchema()
{
    var engine = new MappingRuleEngine();
    var result = engine.FormatTableName("archive.Users", "TestDB");
    
    Assert.Equal("[TestDB].[archive].[Users]", result);
}

[Fact]
public void FormatTableName_AlreadyThreePart_PreservesOriginal()
{
    var engine = new MappingRuleEngine();
    var result = engine.FormatTableName("[OldDB].[dbo].[Users]", "NewDB");
    
    Assert.Equal("[OldDB].[dbo].[Users]", result);
}

[Fact]
public void GenerateMigrationSQL_WithDatabases_IncludesInHeader()
{
    var sql = _ruleEngine.GenerateMigrationSQL(
        config,
        analysisResult,
        datatypeComparisons,
        "SourceDB",
        "DestDB",
        true
    );
    
    Assert.Contains("-- Source Database: SourceDB", sql);
    Assert.Contains("-- Destination Database: DestDB", sql);
}
```

### Integration Tests

1. Test cross-database migration
2. Test same database migration
3. Test without database names (backwards compat)
4. Test with custom schemas
5. Test with linked servers
6. Test with three-part names in input

## Migration Guide

### Updating Existing Code

**Before:**
```csharp
var sql = _ruleEngine.GenerateMigrationSQL(
    config,
    analysisResult,
    datatypeComparisons,
    includeTransaction: true
);
```

**After (with database names):**
```csharp
var sql = _ruleEngine.GenerateMigrationSQL(
    config,
    analysisResult,
    datatypeComparisons,
    sourceDatabase: "OldDB",
    destinationDatabase: "NewDB",
    includeTransaction: true
);
```

**After (backwards compatible):**
```csharp
var sql = _ruleEngine.GenerateMigrationSQL(
    config,
    analysisResult,
    datatypeComparisons,
    includeTransaction: true
);
// Still works! Generates [dbo].[Table] format
```

## Best Practices

### 1. **Always Specify Database Names for Cross-Database**
```csharp
// DO: Explicit is better
GenerateMigrationSQL(..., "SourceDB", "TargetDB", ...)

// DON'T: Implicit in cross-database scenarios
GenerateMigrationSQL(..., "", "", ...)
```

### 2. **Use Connection String Database Name**
```csharp
var sourceDb = new SqlConnectionStringBuilder(sourceConnectionString).InitialCatalog;
var destDb = new SqlConnectionStringBuilder(destConnectionString).InitialCatalog;

GenerateMigrationSQL(..., sourceDb, destDb, ...)
```

### 3. **Omit for Same-Database Migrations**
```csharp
// If source and destination are in same database
GenerateMigrationSQL(..., "", "", ...)
// Cleaner SQL without redundant database names
```

### 4. **Linked Servers**
```csharp
// Format: ServerName.DatabaseName
GenerateMigrationSQL(..., "LINKEDSERVER.RemoteDB", "LocalDB", ...)
```

## Limitations

### 1. Four-Part Names
For linked servers with explicit server names, use database parameter:
```csharp
sourceDatabase: "SERVERNAME.DatabaseName"
```

### 2. Dynamic Databases
Database names are hardcoded in generated SQL:
- Not suitable for parameterized database names
- Solution: Use string replacement after generation

### 3. Permission Requirements
Cross-database queries require:
- User has permissions in both databases
- Databases are on same server (or linked)

## Related Files

- `src\ZaDataStudio.Application\Mapping\MappingRuleEngine.cs` - Main implementation
- `src\ZaDataStudio.Infrastructure\Excel\ExcelMappingService.cs` - Excel service integration
- `src\ZaDataStudio.Web\Components\Pages\SchemaComparison.razor.cs` - UI integration
- `docs\CaseWhen_LookupMapping.md` - Related CASE WHEN feature

## Conclusion

The database name support provides flexible, clear SQL generation for various migration scenarios while maintaining backwards compatibility. By explicitly including database names in the three-part naming convention, the generated SQL is more self-documenting and suitable for cross-database migrations.
