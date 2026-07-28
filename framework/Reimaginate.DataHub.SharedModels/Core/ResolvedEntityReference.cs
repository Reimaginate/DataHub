namespace Reimaginate.DataHub.SharedModels.Core;

public class ResolvedEntityReference
{
    public ExternalEntityReference SourceEntityReference { get; set; }
    public EntityReference DataHubEntityReference { get; set; }
}