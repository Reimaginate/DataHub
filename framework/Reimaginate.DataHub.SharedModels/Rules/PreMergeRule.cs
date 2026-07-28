namespace Reimaginate.DataHub.SharedModels.Rules;

public class PreMergeRule
{
    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public string Context { get; set; } = "*";
    public string Predicate { get; set; }
    public string IfTrue { get; set; }
}