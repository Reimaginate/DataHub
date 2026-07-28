using System.Collections.Generic;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateUntrackedEntities;

public class ProcessUpdateUntrackedEntitiesRequest : IRequest<ProcessUpdateUntrackedEntitiesResponse>
{
    public List<ProcessUpdateUntrackedEntityRequest> Requests { get; set; }

    public bool DispatchNotifications { get; set; }

    public bool Silent { get; set; }
}