using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.CalculateSourceEntityUpdates;

public class CalculateSourceEntityUpdatesRequest : IRequest<CalculateSourceEntityUpdatesResponse>
{
    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public string SourceEntityId { get; set; }
    public JObject SourceEntity { get; set; }

    public ConcurrentBag<ChangeTrackingEntry> ChangeTrackingEntryCache { get; set; }
    // Batch merges use a per-source ordered cache; retain the existing cache for other callers.
    public List<ChangeTrackingEntry> OrderedChangeTrackingEntryCache { get; set; }
    internal bool RetainUnchangedUpdate { get; set; }
}
