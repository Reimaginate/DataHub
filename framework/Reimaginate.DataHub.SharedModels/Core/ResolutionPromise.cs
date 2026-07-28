namespace Reimaginate.DataHub.SharedModels.Core;

public sealed class ResolutionPromise : CosmosDocument
{
    public ResolutionPromise()
    {
        _dt = nameof(ResolutionPromise);
    }

    public string DataHubEntityType { get; set; }
    public string DataHubEntityId { get; set; }
    public string EntityReferencePath { get; set; }
    public ExternalEntityReference ExternalEntityReference { get; set; }
}