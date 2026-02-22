using System;
using System.Collections.Generic;
using System.Text;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping;

public static class LookupExtenstions
{
    public static bool HasSimilarValue(this HashSet<string> lookupValues, LookupValue? value)
    {
        if (value == null)
            return false;
        return lookupValues.Contains(value.EnValue);
    }

    public static bool TryGetSimilarValue(this Dictionary<string, LookupValue> lookupValues,
        LookupValue value, out LookupValue destMatch)
    {
        destMatch = new("", "", "");
        if (value == null)
            return false;
        foreach (string key in lookupValues.Keys)
        {
            if (key == value.EnValue)
            {
                destMatch = lookupValues[key];
                return true;
            }
        }
        return false;
    }
}
