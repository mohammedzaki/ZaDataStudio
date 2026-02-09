# MappingRuleType Enhancement

## Overview
Added a `MappingRuleType` property to the `MappingResult` class to track which mapping rule was applied to each column mapping. This provides better visibility and diagnostics when analyzing mapping results.

## Changes Made

### 1. MappingResult.cs
Added new property:
```csharp
public string MappingRuleType { get; set; } = string.Empty;
```

### 2. Updated All Mapping Rules

All mapping rules now set the `MappingRuleType` property using `nameof()` to ensure type safety:

#### NullMappingRule
```csharp
MappingRuleType = nameof(NullMappingRule)
```

#### ColumnToRowMappingRule
```csharp
MappingRuleType = nameof(ColumnToRowMappingRule)
```

#### ExpressionMappingRule
```csharp
MappingRuleType = nameof(ExpressionMappingRule)
```

#### ConcatenationMappingRule
```csharp
MappingRuleType = nameof(ConcatenationMappingRule)
```

#### ConditionalMappingRule
```csharp
MappingRuleType = nameof(ConditionalMappingRule)
```

#### TypeConversionMappingRule
```csharp
MappingRuleType = nameof(TypeConversionMappingRule)
```

#### DirectMappingRule
```csharp
MappingRuleType = nameof(DirectMappingRule)
```

### 3. MappingRuleEngine.cs
Updated the fallback case to indicate when no rule matched:
```csharp
MappingRuleType = "NoRuleMatched"
```

## Benefits

1. **Diagnostics**: Easy to identify which rule processed each mapping
2. **Debugging**: Helps troubleshoot mapping issues by knowing the rule used
3. **Analytics**: Can analyze which rules are most commonly used
4. **Validation**: Can verify that the correct rule is being applied to mappings
5. **Reporting**: Can include rule type in mapping reports and exports

## Usage Example

```csharp
var result = mappingRuleEngine.ProcessMapping(mapping, context);

Console.WriteLine($"Column: {mapping.NewColumn}");
Console.WriteLine($"Rule Applied: {result.MappingRuleType}");
Console.WriteLine($"SQL Expression: {result.SqlExpression}");
```

### Expected Output for Different Scenarios

#### Scenario 1: Direct Mapping
```
Column: FirstName
Rule Applied: DirectMappingRule
SQL Expression: src.[FirstName]
```

#### Scenario 2: Column-to-Row Mapping
```
Column: SocialMediaPlatformId
Rule Applied: ColumnToRowMappingRule
SQL Expression: -- Column-to-row mapping for SocialMediaPlatformId
```

#### Scenario 3: NULL Mapping
```
Column: DeletedDate
Rule Applied: NullMappingRule
SQL Expression: NULL
```

#### Scenario 4: Type Conversion
```
Column: Age
Rule Applied: TypeConversionMappingRule
SQL Expression: TRY_CAST(src.[Age] AS INT)
```

## Type Safety

Using `nameof()` ensures:
- No typos in rule names
- Automatic updates if class names change
- Compile-time validation
- IntelliSense support

## Future Enhancements

Potential future improvements:
1. Add rule priority to the result
2. Include rule execution time for performance analysis
3. Add rule parameters used (if applicable)
4. Support for rule chaining (multiple rules applied in sequence)
5. Export mapping analysis with rule statistics

## Migration Notes

- This is a non-breaking change
- Existing code will continue to work
- The new property defaults to an empty string
- All existing mapping rules have been updated
- No database schema changes required
