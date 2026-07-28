namespace Reimaginate.DataHub.SharedModels.Rules;

public class PropertyMergeRule
{
    public string PropertyName { get; set; }
    public string Predicate { get; set; }
    public string Action { get; set; }
}