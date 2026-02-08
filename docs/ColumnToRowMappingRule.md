# Column-to-Row Mapping Rule

## Overview
The `ColumnToRowMappingRule` handles unpivot operations where multiple source columns need to be mapped to destination rows with specific ID values.

## Priority
**Priority: 2 (High)** - Runs after `NullMappingRule` but before other transformation rules.

## Use Case
This rule is designed for scenarios where you have:
- Multiple columns in a source table (e.g., Facebook, Twitter, Youtube, LinkedIn, Instagram)
- A destination table with an ID column (e.g., SocialMediaPlatformId)
- Each source column maps to a specific ID value in the destination

## Syntax

The rule detects column-to-row patterns in the `MappingRule` or `Notes` fields using several formats:

### Format 1: Column=Value
```
Facebook=201, Youtube=202, Instagram=203, Linkedin=204, X=205, Snapchat=206
```

### Format 2: Column:Value
```
Facebook:201, Youtube:202, Instagram:203
```

### Format 3: Column->Value
```
Facebook->201, Youtube->202, Instagram->203
```

### Format 4: Value ColumnName (Tab or Space Separated)
```
201	Facebook
202	Youtube
203	Instagram
204	Linkedin
205	X
206	Snapchat
```

### Format 5: Keywords
You can also use explicit keywords:
- `unpivot`
- `column to row`
- `columns to rows`

## Example Usage

### Scenario
You have a source table with social media URL columns:
- Source columns: Facebook, Twitter, Youtube, LinkedIn, Instagram
- Destination: SocialMediaPlatformId (INT NOT NULL)

### Excel Mapping Configuration

| NewColumn | MappingRule or Notes |
|-----------|----------------------|
| SocialMediaPlatformId | 201 Facebook, 202 Youtube, 203 Instagram, 204 Linkedin, 205 X |

### Generated SQL

The rule generates SQL using the **CROSS APPLY** approach with **VALUES** for efficient unpivoting:

```sql
-- Unpivot mapping for SocialMediaPlatformId using CROSS APPLY
INSERT INTO DestinationTable (SocialMediaPlatformId, ... other columns ...)
SELECT
    st.[KeyColumn],    -- replace with your row key column
    m.SocialMediaPlatformId,
    m.Value                        -- replace with actual destination column name
FROM SourceTable AS st
CROSS APPLY (
    VALUES
        (201, st.[Facebook]),
        (202, st.[Youtube]),
        (203, st.[Instagram]),
        (204, st.[Linkedin]),
        (205, st.[X])
) AS m(SocialMediaPlatformId, Value)
WHERE
    m.Value IS NOT NULL
    AND NULLIF(LTRIM(RTRIM(m.Value)), '') IS NOT NULL;
```

#### Why CROSS APPLY?

This approach is more efficient than UNION ALL because:
- Single table scan instead of multiple scans
- Better query optimization by SQL Server
- More readable and maintainable code
- Automatically handles the row replication logic

## NULL Handling

The rule respects the `NewColumnNullable` property and includes additional string trimming:

- **If destination column does NOT allow NULL** (`NewColumnNullable = false`):
  - Adds `WHERE m.Value IS NOT NULL` filter
  - Includes `NULLIF(LTRIM(RTRIM(m.Value)), '')` to filter out empty strings
  - Only inserts rows where the source column has actual content

- **If destination column allows NULL** (`NewColumnNullable = true`):
  - No WHERE clause added
  - Inserts all rows regardless of NULL or empty values

## Dependencies

The rule automatically tracks all source columns as dependencies in the `MappingResult.Dependencies` list.

## Notes

1. The generated SQL includes placeholder comments (`... other columns ...`) that need to be replaced with actual column mappings
2. You'll need to manually adjust the generated SQL to include proper source column references
3. The rule works best when combined with other mapping rules for the remaining columns
4. Consider creating a stored procedure or script template for complex unpivot operations

## Example Complete Scenario

### Source Table: Company
| CompanyId | Facebook | Twitter | Youtube | LinkedIn |
|-----------|----------|---------|---------|----------|
| 1 | fb.com/company1 | NULL | yt.com/company1 | NULL |
| 2 | NULL | twitter.com/c2 | NULL | linkedin.com/c2 |

### Destination Table: CompanySocialMedia
| CompanyId | SocialMediaPlatformId | URL |
|-----------|----------------------|-----|
| 1 | 201 | fb.com/company1 |
| 1 | 202 | yt.com/company1 |
| 2 | 203 | twitter.com/c2 |
| 2 | 204 | linkedin.com/c2 |

### Mapping Configuration
```
MappingRule: "201 Facebook, 205 Twitter, 202 Youtube, 204 LinkedIn"
NewColumnNullable: false
```

### Generated SQL
```sql
-- Unpivot mapping for SocialMediaPlatformId using CROSS APPLY
INSERT INTO CompanySocialMedia (SocialMediaPlatformId, ... other columns ...)
SELECT
    c.[CompanyId],    -- replace with your row key column
    m.SocialMediaPlatformId,
    m.Value                        -- replace with actual destination column name (URL)
FROM Company AS c
CROSS APPLY (
    VALUES
        (201, c.[Facebook]),
        (205, c.[Twitter]),
        (202, c.[Youtube]),
        (204, c.[LinkedIn])
) AS m(SocialMediaPlatformId, Value)
WHERE
    m.Value IS NOT NULL
    AND NULLIF(LTRIM(RTRIM(m.Value)), '') IS NOT NULL;
```

This will generate SQL that creates multiple rows per company, one for each non-NULL social media platform.
