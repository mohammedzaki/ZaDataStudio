using System;
using System.Collections.Generic;
using System.Text;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping
{
    public interface IMappingValidator
    {
        public ValidationReport Validate(DataMappingConfiguration config);
    }
}
