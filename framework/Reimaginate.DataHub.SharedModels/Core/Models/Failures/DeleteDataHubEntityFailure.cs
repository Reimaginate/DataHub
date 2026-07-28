using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Core.Models.Failures;

public class DeleteDataHubEntityFailure
{
    public JObject DataHubEntity { get; set; }
    public string FailureReason { get; set; }
}