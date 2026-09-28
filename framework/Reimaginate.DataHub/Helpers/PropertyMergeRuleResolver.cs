using System;
using System.Linq;
using Reimaginate.DataHub.SharedModels.Rules;

namespace Reimaginate.DataHub.Helpers;

internal static class PropertyMergeRuleResolver
{
    // A child can override its parent; only complete dot-delimited ancestors match.
    internal static PropertyMergeRule Resolve(MergeRule mergeRule, string propertyPath)
    {
        var candidate = propertyPath;
        while (true)
        {
            var rule = mergeRule.Rules.FirstOrDefault(r =>
                string.Equals(r.PropertyName, candidate, StringComparison.CurrentCultureIgnoreCase));
            if (rule != null) return rule;

            var separator = candidate.LastIndexOf('.');
            if (separator < 0) break;
            candidate = candidate.Substring(0, separator);
        }

        return mergeRule.Rules.FirstOrDefault(r => r.PropertyName == "*");
    }
}
