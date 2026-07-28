namespace Reimaginate.DataHub.SharedModels.Rules;

public class DuplicatePreventionRule
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string DataSource { get; set; }
    public string Type { get; set; }
    public string Find { get; set; }
    public string Match { get; set; }
}