using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Converters;

namespace Reimaginate.DataHub.SharedModels.Core;

public class ChangeTrackingEntry : CosmosDocument
{
    public ChangeTrackingEntry()
    {
        _dt = nameof(ChangeTrackingEntry);
    }

    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public string EntryType { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    [JsonConverter(typeof(DataHubEntityConverter))]
    public JObject Data { get; set; }
}