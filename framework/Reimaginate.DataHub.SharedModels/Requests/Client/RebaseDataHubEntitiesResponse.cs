using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RebaseDataHubEntitiesResponse
{
    public List<RebaseEntityTrackingResult> Results { get; set; } = new();
}