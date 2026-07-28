using System.Collections.Generic;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;

public class AddTrackedEntityChangeSetsRequest : IRequest<AddTrackedEntityChangeSetsResponse>
{
    public List<AddTrackedEntityChangeSetRequest> Requests { get; set; }
}