using System;
using System.Collections.Generic;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRebaseDataHubEntities;

public class ProcessRebaseDataHubEntitiesRequest : IRequest<ProcessRebaseDataHubEntitiesResponse>
{
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
    public DateTimeOffset? RebaseTo { get; set; }
}