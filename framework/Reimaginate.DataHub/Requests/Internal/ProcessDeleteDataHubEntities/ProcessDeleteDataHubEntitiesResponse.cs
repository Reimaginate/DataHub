using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;

namespace Reimaginate.DataHub.Requests.Internal.ProcessDeleteDataHubEntities;

public class ProcessDeleteDataHubEntitiesResponse
{
    public bool Success { get; set; }
    public List<DeleteDataHubEntityFailure> Failures { get; set; } = new();
}