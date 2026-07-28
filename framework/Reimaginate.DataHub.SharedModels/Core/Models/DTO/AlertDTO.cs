using Newtonsoft.Json.Linq;
using System;

namespace Reimaginate.DataHub.SharedModels.Core.Models.DTO;

public class AlertDTO
{
    public string Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Severity { get; set; }
    public string Subject { get; set; }
    public string Description { get; set; }
    public JToken Data { get; set; }
}