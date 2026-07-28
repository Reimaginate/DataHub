using System;

namespace Reimaginate.DataHub.Helpers;

public static class StringExtensions
{
    public static string EscapeCosmosSpecialChars(this string val)
    {
        return val?.Replace("'", "\\'");
    }

    public static DateTimeOffset? ToDateTimeOffset(this string val)
    {
        if (string.IsNullOrEmpty(val)) return null;
        var ret = DateTimeOffset.Parse(val);
        return ret;
    }
}