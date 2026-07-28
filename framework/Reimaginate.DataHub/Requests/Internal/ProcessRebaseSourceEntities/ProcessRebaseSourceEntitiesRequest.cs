using System;
using System.Collections.Generic;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRebaseSourceEntities;

public class ProcessRebaseSourceEntitiesRequest : IRequest<ProcessRebaseSourceEntitiesResponse>
{
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
    public DateTimeOffset? RebaseTo { get; set; }
}