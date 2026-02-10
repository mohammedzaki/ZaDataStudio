# Values Mapping Feature - Lookup Analysis Enhancement

## Overview
Enhanced the lookup column analysis to include a detailed values mapping table showing the relationship between source and destination lookup values, making it easy to identify which values match and which are missing in the destination.

## Changes Made

### 1. Updated `LookupColumnAnalyzer` Class

**File:** `src\ZaDataStudio.Application\Mapping\LookupColumnAnalyzer.cs`

#### Added `BuildValuesMapping` Method
```csharp
private void BuildValuesMapping(LookupColumnAnalysis analysis)
{
    analysis.ValuesMapping.Clear();

    // Create a dictionary for quick destination lookup by value (case-insensitive)
    var destByValue = new Dictionary<string, (string code, string value)>(StringComparer.OrdinalIgnoreCase);
    foreach (var dest in analysis.DestinationSampleValues)
    {
        if (!destByValue.ContainsKey(dest.Value))
        {
            destByValue[dest.Value] = (dest.Key, dest.Value);
        }
    }

    // Map all source values
    foreach (var source in analysis.SourceSampleValues)
    {
        var mapping = new LookupValueMapping
        {
            SourceCode = source.Key,
            SourceValue = source.Value
        };

        // Try to find matching destination value (case-insensitive)
        if (destByValue.TryGetValue(source.Value, out var destMatch))
        {
            mapping.DestinationCode = destMatch.code;
            mapping.DestinationValue = destMatch.value;
        }
        else
        {
            // No match found - set destination as empty/null
            mapping.DestinationCode = string.Empty;
            mapping.DestinationValue = string.Empty;
        }

        analysis.ValuesMapping.Add(mapping);
    }
}
```

#### Integration Points
- Called in `AnalyzeLookupColumnAsync()` after finding mismatched values
- Called in `AnalyzeLookupColumnWithSpecAsync()` after comparing values
- Populates the `ValuesMapping` property with all source values and their destination matches

### 2. Enhanced UI - SchemaComparison.razor

**File:** `src\ZaDataStudio.Web\Components\Pages\SchemaComparison.razor`

#### Added Values Mapping Table Display

**Features:**
- **Scrollable Table**: Max height 400px with sticky header and footer
- **Color Coding**: Yellow background for unmatched rows
- **Status Icons**: 
  - ✓ Green arrow for matched values
  - ⚠️ Yellow X for missing values
- **Summary Footer**: Shows count and percentage of matched/missing values
- **Responsive Layout**: Works well on different screen sizes

**Table Columns:**
1. **Source Code**: The key/code from the source lookup table
2. **Source Value**: The display value from source
3. **Arrow/Status**: Visual indicator of match status
4. **Destination Code**: The matching code in destination (or "-" if no match)
5. **Destination Value**: The matching value in destination (or "No match")
6. **Status Badge**: "Matched" (green) or "Missing" (yellow)

#### CSS Styling
Added custom styles for:
- Sticky table headers and footers
- Better scrollable table appearance
- Hover effects for rows
- Warning row highlighting
- Badge styling

## Usage Example

### Scenario: Social Media Platform Lookup

**Source Lookup Table:**
| Code | Name |
|------|------|
| 1 | Facebook |
| 2 | Twitter |
| 3 | Instagram |
| 4 | LinkedIn |
| 5 | YouTube |

**Destination Lookup Table:**
| Code | Name |
|------|------|
| 201 | Facebook |
| 202 | YouTube |
| 203 | Instagram |
| 204 | LinkedIn |

### Analysis Result

The Values Mapping table will show:

| Source Code | Source Value | → | Destination Code | Destination Value | Status |
|-------------|--------------|---|------------------|-------------------|---------|
| 1 | Facebook | ✓ | 201 | Facebook | ✅ Matched |
| 2 | Twitter | ⚠️ | - | *No match* | ⚠️ Missing |
| 3 | Instagram | ✓ | 203 | Instagram | ✅ Matched |
| 4 | LinkedIn | ✓ | 204 | LinkedIn | ✅ Matched |
| 5 | YouTube | ✓ | 202 | YouTube | ✅ Matched |

**Summary:** 4 Matched, 1 Missing (80.0%)

## Features

### 1. **Case-Insensitive Matching**
Values are matched using case-insensitive comparison, so "Facebook" matches "FACEBOOK"

### 2. **Missing Value Detection**
Clearly highlights values that exist in source but not in destination with:
- Yellow background row
- "No match" text in destination columns
- Warning badge

### 3. **Summary Statistics**
Footer shows:
- Total count of matched values
- Total count of missing values
- Percentage of successful matches

### 4. **Performance Optimized**
Uses dictionary lookup for O(1) matching complexity instead of nested loops

### 5. **User-Friendly Display**
- Icons and colors for quick visual assessment
- Scrollable for large datasets
- Sticky headers to maintain context while scrolling

## Benefits

### For Developers
1. **Quick Identification**: Immediately see which lookup values are problematic
2. **Data Migration Planning**: Know exactly which values need to be added to destination
3. **Testing Verification**: Validate that lookup tables are properly synchronized

### For Business Users
1. **Visual Clarity**: Color-coded display makes it easy to spot issues
2. **Comprehensive View**: See all mappings in one place
3. **Summary Metrics**: Quick percentage gives overall health status

## Integration

### Backend
```csharp
// Automatically called by LookupColumnAnalyzer
var analysis = await _lookupAnalyzer.AnalyzeLookupColumnWithSpecAsync(
    columnMapping,
    sourceConnectionString,
    destinationConnectionString);

// ValuesMapping is populated automatically
var matchedCount = analysis.ValuesMapping.Count(m => !string.IsNullOrEmpty(m.DestinationValue));
var missingCount = analysis.ValuesMapping.Count(m => string.IsNullOrEmpty(m.DestinationValue));
```

### Frontend
```razor
@if (lookup.ValuesMapping.Any())
{
    <div class="mt-3">
        <h6>
            <span class="bi bi-arrow-left-right me-2"></span>
            Values Mapping (@lookup.ValuesMapping.Count values)
        </h6>
        <!-- Table renders automatically -->
    </div>
}
```

## Example Screenshots

### Matched Values
- Green arrow (→)
- Normal white background
- ✅ "Matched" badge

### Missing Values
- Yellow X (✖)
- Yellow background
- ⚠️ "Missing" badge
- "No match" text in destination

### Summary Footer
```
Summary: [4 Matched] [1 Missing] | 80.0%
```

## Technical Details

### Data Structure
```csharp
public class LookupValueMapping
{
    public string SourceCode { get; set; } = string.Empty;
    public string SourceValue { get; set; } = string.Empty;
    public string DestinationCode { get; set; } = string.Empty;
    public string DestinationValue { get; set; } = string.Empty;
}
```

### Matching Algorithm
1. Build dictionary of destination values (key = value, value = (code, value))
2. For each source value:
   - Try to find in destination dictionary (case-insensitive)
   - If found: populate destination code and value
   - If not found: leave destination fields empty
3. Add mapping to results list

### Performance Characteristics
- Time Complexity: O(n + m) where n = source values, m = destination values
- Space Complexity: O(m) for destination dictionary
- Memory efficient for large lookup tables

## Future Enhancements

1. **Export Functionality**
   - Export ValuesMapping to Excel
   - Export missing values for data team

2. **Filtering**
   - Filter to show only missing values
   - Filter to show only matched values
   - Search by code or value

3. **Bulk Actions**
   - Generate INSERT statements for missing values
   - Generate mapping SQL scripts
   - Copy missing values to clipboard

4. **Advanced Matching**
   - Fuzzy matching for similar values
   - Suggest potential matches
   - Custom matching rules

5. **Visualization**
   - Chart showing match percentage
   - Trend over multiple analyses
   - Compare multiple lookup tables

## Testing

### Unit Tests
```csharp
[Fact]
public void BuildValuesMapping_AllMatched_PopulatesCorrectly()
{
    // Arrange
    var analysis = new LookupColumnAnalysis();
    analysis.SourceSampleValues = new() { {"1", "Facebook"}, {"2", "Twitter"} };
    analysis.DestinationSampleValues = new() { {"201", "Facebook"}, {"202", "Twitter"} };
    
    // Act
    BuildValuesMapping(analysis);
    
    // Assert
    Assert.Equal(2, analysis.ValuesMapping.Count);
    Assert.All(analysis.ValuesMapping, m => Assert.NotEmpty(m.DestinationValue));
}

[Fact]
public void BuildValuesMapping_WithMissing_IdentifiesCorrectly()
{
    // Arrange
    var analysis = new LookupColumnAnalysis();
    analysis.SourceSampleValues = new() { {"1", "Facebook"}, {"2", "TikTok"} };
    analysis.DestinationSampleValues = new() { {"201", "Facebook"} };
    
    // Act
    BuildValuesMapping(analysis);
    
    // Assert
    Assert.Equal(2, analysis.ValuesMapping.Count);
    Assert.Single(analysis.ValuesMapping, m => string.IsNullOrEmpty(m.DestinationValue));
}
```

### Integration Tests
- Test with real database connections
- Verify UI rendering
- Test scrolling behavior
- Verify sticky headers work

## Related Files

- `src\ZaDataStudio.Application\Mapping\LookupColumnAnalyzer.cs` - Backend logic
- `src\ZaDataStudio.Web\Components\Pages\SchemaComparison.razor` - UI display
- `src\ZaDataStudio.Domain\Entities\LookupColumnAnalysis.cs` - Data model
- `docs\LookupColumnAnalyzer_Refactoring.md` - Original refactoring doc

## Conclusion

This enhancement provides a comprehensive view of lookup value mappings, making it easy to identify data synchronization issues and plan data migration activities. The combination of clear visual indicators, summary statistics, and detailed row-by-row comparison creates an effective tool for data quality assurance.
