using System;
using System.Collections.Generic;
using System.Text;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// Base interface for mapping rules (Strategy pattern)
/// </summary>
public interface IMappingRule
{
    int Priority { get; }
    bool CanHandle(DataColumnMapping mapping);
    MappingResult Apply(DataColumnMapping mapping, MappingContext context);
}
