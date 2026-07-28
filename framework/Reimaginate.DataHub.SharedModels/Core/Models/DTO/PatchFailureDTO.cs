using Newtonsoft.Json.Linq;
using System;

namespace Reimaginate.DataHub.SharedModels.Core.Models.DTO;

public class PatchFailureDTO
{
    public string Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventSource { get; set; }
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public JToken Patch { get; set; }
    public string FailureReason { get; set; }
}