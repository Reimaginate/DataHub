using Newtonsoft.Json.Linq;
using System;

namespace Reimaginate.DataHub.SharedModels.Core.Models.DTO;

public class JobDTO
{
    public string JobId { get; set; }
    public string Type { get; set; }
    public string Name { get; set; }
    public string Target { get; set; }
    public UserRef CreatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset? CompletedOn { get; set; }
    public DateTimeOffset? LastUpdated { get; set; }
    public JToken Request { get; set; }
    public JToken Response { get; set; }
    public string Status { get; set; }

}