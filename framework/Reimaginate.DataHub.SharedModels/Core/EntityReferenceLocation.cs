namespace Reimaginate.DataHub.SharedModels.Core;

public sealed class EntityReferenceLocation 
{
    public string DataHubEntityType { get; set; }
    public string DataHubEntityId { get; set; }
    public string EntityReferencePath { get; set; }
    public ExternalEntityReference ExternalEntityReference { get; set; }
}