using System.Collections.Generic;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.InitTrackedEntities;

public class InitTrackedEntitiesRequest : IRequest<InitTrackedEntitiesResponse>
{
    public List<InitTrackedEntityRequest> Requests { get; set; }
}