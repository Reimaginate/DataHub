using System.Collections.Generic;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;

namespace Reimaginate.DataHub.Requests.Internal.CalculateSourceEntityUpdates;

public class CalculateSourceEntityUpdatesResponse
{
    public List<InitTrackedEntityRequest> ResultingInitTrackedEntityRequests { get; set; } = new();
    public List<AddTrackedEntityChangeSetRequest> ResultingSourceEntityUpdates { get; set; } = new();
}