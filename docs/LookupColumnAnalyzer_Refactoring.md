# Lookup Column Analyzer Refactoring

## Overview
Extracted lookup column analysis logic from `MappingComparisonService` into a dedicated `LookupColumnAnalyzer` class following the Single Responsibility Principle and improving code organization.

## Changes Made

### 1. Created `ILookupColumnAnalyzer` Interface

**File:** `src\ZaDataStudio.Application\Mapping\ILookupColumnAnalyzer.cs`

```csharp
public interface ILookupColumnAnalyzer
{
    Task<LookupColumnAnalysis> AnalyzeLookupColumnAsync(
        DataColumnMapping columnMapping,
        string sourceConnectionString,
        string destinationConnectionString);

    Task<LookupColumnAnalysis> AnalyzeLookupColumnWithSpecAsync(
        DataColumnMapping columnMapping,
        string sourceConnectionString,
        string destinationConnectionString);
}
```

### 2. Implemented `LookupColumnAnalyzer` Class

**File:** `src\ZaDataStudio.Application\Mapping\LookupColumnAnalyzer.cs`

Moved the following methods from `MappingComparisonService`:
- `AnalyzeLookupColumn()` → `AnalyzeLookupColumnAsync()`
- `AnalyzeLookupColumnWithSpec()` → `AnalyzeLookupColumnWithSpecAsync()`
- `LoadLookupData()` → `LoadLookupDataAsync()`
- `FormatTableName()` (private helper)

### 3. Updated `MappingComparisonService`

**Changes:**
- Removed `_ruleEngine` field (now in `LookupColumnAnalyzer`)
- Added `ILookupColumnAnalyzer` dependency injection
- Updated constructor to inject `ILookupColumnAnalyzer`
- Replaced direct method calls with analyzer calls:
  - `await AnalyzeLookupColumnWithSpec(...)` → `await _lookupAnalyzer.AnalyzeLookupColumnWithSpecAsync(...)`
  - `await AnalyzeLookupColumn(...)` → `await _lookupAnalyzer.AnalyzeLookupColumnAsync(...)`
- Removed lookup-related private methods (moved to analyzer)

## Benefits

### 1. **Single Responsibility Principle**
- `MappingComparisonService` now focuses on orchestrating comparisons
- `LookupColumnAnalyzer` focuses solely on lookup analysis logic

### 2. **Better Testability**
```csharp
// Easy to mock the analyzer in tests
var mockAnalyzer = new Mock<ILookupColumnAnalyzer>();
var service = new MappingComparisonService(dbService, mockAnalyzer);
```

### 3. **Code Reusability**
The lookup analyzer can now be used independently:
```csharp
var analyzer = new LookupColumnAnalyzer(dbService);
var analysis = await analyzer.AnalyzeLookupColumnWithSpecAsync(mapping, srcConn, destConn);
```

### 4. **Improved Maintainability**
- Lookup-related logic is now in one place
- Easier to locate and modify lookup analysis code
- Clear separation of concerns

### 5. **Dependency Injection Ready**
Can be registered in DI container:
```csharp
services.AddScoped<ILookupColumnAnalyzer, LookupColumnAnalyzer>();
services.AddScoped<IMappingComparisonService, MappingComparisonService>();
```

## Architecture

### Before
```
MappingComparisonService
├── CompareMappingsAsync()
├── AnalyzeLookupColumn()           ← Mixed responsibilities
├── AnalyzeLookupColumnWithSpec()   ← Mixed responsibilities
├── LoadLookupData()                ← Mixed responsibilities
├── CompareDatatypes()
└── Helper methods...
```

### After
```
MappingComparisonService
├── CompareMappingsAsync()
├── CompareDatatypes()
└── Helper methods...

LookupColumnAnalyzer (New)
├── AnalyzeLookupColumnAsync()
├── AnalyzeLookupColumnWithSpecAsync()
├── LoadLookupDataAsync()
└── FormatTableName()
```

## Usage Example

### Old Code (Before Refactoring)
```csharp
public class MappingComparisonService
{
    private MappingRuleEngine _ruleEngine;
    
    public MappingComparisonService(IDatabaseService databaseService)
    {
        _ruleEngine = new MappingRuleEngine();
        _databaseService = databaseService;
    }
    
    // Lookup analysis was done internally
    var analysis = await AnalyzeLookupColumnWithSpec(mapping);
}
```

### New Code (After Refactoring)
```csharp
public class MappingComparisonService
{
    private readonly ILookupColumnAnalyzer _lookupAnalyzer;
    
    public MappingComparisonService(
        IDatabaseService databaseService,
        ILookupColumnAnalyzer lookupAnalyzer)
    {
        _databaseService = databaseService;
        _lookupAnalyzer = lookupAnalyzer;
    }
    
    // Lookup analysis delegated to analyzer
    var analysis = await _lookupAnalyzer.AnalyzeLookupColumnWithSpecAsync(
        mapping,
        _sourceConnectionString,
        _destinationConnectionString);
}
```

## Dependency Injection Setup

### Registration in DI Container
```csharp
// In Program.cs or Startup.cs
services.AddScoped<ILookupColumnAnalyzer, LookupColumnAnalyzer>();
services.AddScoped<IMappingComparisonService, MappingComparisonService>();
services.AddScoped<IDatabaseService, DatabaseService>();
```

### Constructor Injection
```csharp
public class SomeController
{
    private readonly ILookupColumnAnalyzer _analyzer;
    
    public SomeController(ILookupColumnAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }
    
    public async Task<IActionResult> AnalyzeLookup()
    {
        var analysis = await _analyzer.AnalyzeLookupColumnWithSpecAsync(...);
        return Ok(analysis);
    }
}
```

## Testing

### Unit Testing the Analyzer
```csharp
[Fact]
public async Task AnalyzeLookupColumnAsync_ReturnsCorrectAnalysis()
{
    // Arrange
    var mockDbService = new Mock<IDatabaseService>();
    mockDbService.Setup(db => db.ExecuteReaderAsync(...))
        .ReturnsAsync(mockReader);
    
    var analyzer = new LookupColumnAnalyzer(mockDbService.Object);
    var mapping = new DataColumnMapping { ... };
    
    // Act
    var result = await analyzer.AnalyzeLookupColumnAsync(
        mapping, srcConn, destConn);
    
    // Assert
    Assert.NotNull(result);
    Assert.True(result.SourceSampleValues.Count > 0);
}
```

### Integration Testing
```csharp
[Fact]
public async Task MappingComparisonService_UsesAnalyzerCorrectly()
{
    // Arrange
    var mockAnalyzer = new Mock<ILookupColumnAnalyzer>();
    mockAnalyzer.Setup(a => a.AnalyzeLookupColumnWithSpecAsync(...))
        .ReturnsAsync(expectedAnalysis);
    
    var service = new MappingComparisonService(dbService, mockAnalyzer.Object);
    
    // Act
    var result = await service.CompareMappingsAsync(...);
    
    // Assert
    mockAnalyzer.Verify(a => a.AnalyzeLookupColumnWithSpecAsync(...), Times.Once);
}
```

## Migration Notes

### For Existing Code
1. Update DI registrations to include `ILookupColumnAnalyzer`
2. No changes needed in calling code (same public API)
3. Internal implementation is now delegated to analyzer

### Breaking Changes
**None** - This is an internal refactoring that doesn't affect the public API of `MappingComparisonService`.

## Future Enhancements

1. **Add Caching**: Cache lookup analysis results for better performance
2. **Parallel Analysis**: Analyze multiple lookups in parallel
3. **Batch Operations**: Analyze multiple columns in one database call
4. **Progress Reporting**: Add progress callbacks for long-running analyses
5. **Validation**: Add input validation before analysis
6. **Error Handling**: Improve error messages and recovery

## Related Files

- `src\ZaDataStudio.Application\Mapping\ILookupColumnAnalyzer.cs` (New)
- `src\ZaDataStudio.Application\Mapping\LookupColumnAnalyzer.cs` (New)
- `src\ZaDataStudio.Application\Mapping\MappingComparisonService.cs` (Modified)
- `src\ZaDataStudio.Application\Mapping\LookupSpecificationParser.cs` (Used by analyzer)
- `src\ZaDataStudio.Application\Mapping\MappingRuleEngine.cs` (Used by analyzer)
- `src\ZaDataStudio.Domain\Entities\LookupColumnAnalysis.cs` (Return type)

## Performance Impact

**No negative performance impact:**
- Same database queries
- Same logic flow
- Minimal additional method call overhead (negligible)
- Better maintainability leads to easier optimization in the future

## Conclusion

This refactoring improves code organization, testability, and maintainability while maintaining the same functionality and performance. The lookup analysis logic is now properly encapsulated in its own class with a clear interface, making it easier to understand, test, and modify.
