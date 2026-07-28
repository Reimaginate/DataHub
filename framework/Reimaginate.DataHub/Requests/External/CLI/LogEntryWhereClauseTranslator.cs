using System;
using System.Linq;
using System.Text.RegularExpressions;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;

namespace Reimaginate.DataHub.Requests.External.CLI;

internal static class LogEntryWhereClauseTranslator
{
    public static string TranslateEventAliases<TEvent>(string whereClause)
        where TEvent : Event
    {
        if (string.IsNullOrWhiteSpace(whereClause))
        {
            return whereClause;
        }

        var translated = ReplaceProperty(whereClause, "Id", "id");
        foreach (var property in typeof(TEvent).GetProperties().OrderByDescending(property => property.Name.Length))
        {
            translated = ReplaceProperty(translated, property.Name, $"Data.{property.Name}");
        }

        return translated;
    }

    private static string ReplaceProperty(string whereClause, string fromPropertyName, string toPropertyPath)
    {
        return Regex.Replace(
            whereClause,
            $@"(?<![\w.])x\.{Regex.Escape(fromPropertyName)}(?!\w)",
            $"x.{toPropertyPath}",
            RegexOptions.CultureInvariant);
    }
}
