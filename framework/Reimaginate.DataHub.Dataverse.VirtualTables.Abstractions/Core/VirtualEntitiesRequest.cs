using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Dataverse.VirtualTables.Abstractions.Core;

public abstract class VirtualEntitiesRequest<TResponse> : IRequest<TResponse>
{
    public string RequestType { get; set; } = null!;
}