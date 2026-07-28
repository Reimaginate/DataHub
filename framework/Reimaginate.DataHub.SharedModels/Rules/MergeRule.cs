using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Rules;

public class MergeRule
{
    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public string Context { get; set; } = "*";
    public List<PropertyMergeRule> Rules { get; set; } = new();
}