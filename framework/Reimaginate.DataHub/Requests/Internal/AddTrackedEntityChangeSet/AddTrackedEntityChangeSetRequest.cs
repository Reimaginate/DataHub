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
}