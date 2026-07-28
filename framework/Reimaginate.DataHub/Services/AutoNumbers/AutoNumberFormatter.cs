using System;
using System.Globalization;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Services.AutoNumbers;

internal static class AutoNumberFormatter
{
    public static string Format(AutoNumberSequence sequence, long sequenceValue)
    {
        var number = FormatNumber(sequenceValue, sequence.PaddingLength);
        return $"{sequence.Prefix}{number}{sequence.Suffix}";
    }

    private static string FormatNumber(long sequenceValue, int paddingLength)
    {
        if (paddingLength <= 0)
        {
            return sequenceValue.ToString(CultureInfo.InvariantCulture);
        }

        var sign = sequenceValue < 0 ? "-" : string.Empty;
        var absoluteValue = Math.Abs(sequenceValue).ToString(CultureInfo.InvariantCulture);
        return sign + absoluteValue.PadLeft(paddingLength, '0');
    }
}
