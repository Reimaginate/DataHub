namespace Reimaginate.DataHub.VirtualEntities.Abstractions.Core;

public class VirtualEntitiesMessage
{
    public string RequestType { get; set; } = null!;
    public string Data { get; set; } = null!;
}