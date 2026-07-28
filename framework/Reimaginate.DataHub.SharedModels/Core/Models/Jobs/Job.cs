using System;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Core.Models.Jobs;

public class Job : ManagementEntry
{
    public Job()
    {
        _dt = "Job";
    }
    public DateTimeOffset? CompletedOn { get; set; }
    public JToken Request { get; set; }
    public JToken Response { get; set; }
    public string LastUpdatedBy { get; set; }
    public string Name { get; set; }
    public string Status { get; set; }
    public string Target { get; set; }
    public string Type { get; set; }
    public UserRef CreatedBy { get; set; }
}