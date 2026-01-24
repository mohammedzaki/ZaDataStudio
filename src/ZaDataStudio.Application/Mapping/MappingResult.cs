using System;
using System.Collections.Generic;
using System.Text;

namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// Result of applying a mapping rule
/// </summary>
public class MappingResult
{
    public string SqlExpression { get; set; } = string.Empty;
    public bool HasWarning { get; set; }
    public string Warning { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new();
}
