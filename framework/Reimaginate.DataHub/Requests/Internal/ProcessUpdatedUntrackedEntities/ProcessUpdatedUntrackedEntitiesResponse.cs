using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdatedUntrackedEntities;

public class ProcessUpdatedUntrackedEntitiesResponse
{
    public List<MergeEntityResult> Results { get; set; } = new();
}