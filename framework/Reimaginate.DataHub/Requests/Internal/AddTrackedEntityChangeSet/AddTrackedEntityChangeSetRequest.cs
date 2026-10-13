using System;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSet;

public class AddTrackedEntityChangeSetRequest : IRequest<ChangeTrackingEntry>
{
    public string EntityType { get; set; }
    public string DataSource { get; set; }
    public string SourceEntityId { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
    public JObject ChangeSet { get; set; }
    // The merge input that produced this delta, used when applying alternate keys.
    public JArray SourceEntityAlternateKeys { get; set; }
    // Batch-local outcome, never persisted in the tracking document.
    internal bool ExcludeFromSourceHistory { get; set; }
    // Immutable input reference retained only for lazy rebasing after a rejected event.
    internal JObject SourceEntityInput { get; set; }
}
