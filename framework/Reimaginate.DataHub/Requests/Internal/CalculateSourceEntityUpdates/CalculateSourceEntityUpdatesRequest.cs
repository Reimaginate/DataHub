using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
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
}