using System;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.CreateChangeTrackingEntry;

public class CreateChangeTrackingEntryRequest : IRequest<ChangeTrackingEntry>
{
    public string EntryType { get; set; }
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public JObject Data { get; set; }
    public bool SaveNow { get; set; } = true;
}