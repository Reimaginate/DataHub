using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.RegisterJobs;

public class RegisterJobRequest
{
    public string JobName { get; set; }
    public string JobId { get; set; }
    public string Target { get; set; }
    public string JobType { get; set; }
    public JToken Request { get; set; }
    public User User { get; set; }
    public string Status { get; set; }
}