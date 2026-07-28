using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetDataHubEntityTypeCountsResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<EntityTypeCount> Results { get; set; } = new();
}