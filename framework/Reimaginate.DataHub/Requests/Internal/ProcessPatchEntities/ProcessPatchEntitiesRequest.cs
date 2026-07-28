using System.Collections.Generic;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessPatchEntities;

public class ProcessPatchEntitiesRequest : IRequest<ProcessPatchEntitiesResponse>
{
    public string CorrelationId { get; set; }
    public List<ProcessPatchEntityRequest> Requests { get; set; }
    public bool Silent { get; set; }
    public bool DispatchNotifications { get; set; }
    public bool DoNotTrack { get; set; }
}
