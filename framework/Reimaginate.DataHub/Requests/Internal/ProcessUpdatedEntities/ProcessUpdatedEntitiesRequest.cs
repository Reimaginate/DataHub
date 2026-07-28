using System.Collections.Generic;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdatedEntities;

public class ProcessUpdatedEntitiesRequest : IRequest<ProcessUpdatedEntitiesResponse>
{
    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public string DataHubEntityType { get; set; }
    public List<AddTrackedEntityChangeSetRequest> SourceEntityChangesWithAlternateKeys { get; set; }
    public List<ChangeTrackingEntry> ConvertedSourceEntityChanges { get; set; }
    public List<MergeEntityRequest> MergeRequests { get; set; }
    public List<ResolvedEntityReference> ResolvedReferencedEntities { get; set; }
    public string CorrelationId { get; set; }
    public EntityConfig EntityConfig { get; set; }
    public bool DoNotTrack { get; set; } = false;
}