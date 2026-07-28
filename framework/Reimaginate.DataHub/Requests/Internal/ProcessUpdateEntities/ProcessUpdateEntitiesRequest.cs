using System.Collections.Generic;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateEntities;

public class ProcessUpdateEntitiesRequest : IRequest<ProcessUpdateEntitiesResponse>
{
    public List<ProcessUpdateEntityRequest> Requests { get; set; }
}