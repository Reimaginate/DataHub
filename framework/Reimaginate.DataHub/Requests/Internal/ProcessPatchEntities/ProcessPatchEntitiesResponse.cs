using System.Collections.Generic;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;

namespace Reimaginate.DataHub.Requests.Internal.ProcessPatchEntities;

public class ProcessPatchEntitiesResponse
{
    public List<ProcessPatchEntityResponse> Results { get; set; }
}