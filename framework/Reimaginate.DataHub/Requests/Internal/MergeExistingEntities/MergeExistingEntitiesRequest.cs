using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.MergeExistingEntities;

public class MergeExistingEntitiesRequest : IRequest<MergeExistingEntitiesResponse>
{
    public string DataSource { get; set; }
    public string DataHubEntityType { get; set; }
    public List<MergeEntityRequest> MergeRequests { get; set; }
    public List<ResolvedEntityReference> ResolvedDataHubEntities { get; set; }
    public string CorrelationId { get; set; }
    public string SourceEntityType { get; set; }
    public EntityConfig EntityConfig { get; set; }
}