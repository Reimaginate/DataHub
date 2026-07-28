using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.MergeExistingUntrackedEntities;

public class MergeExistingUntrackedEntitiesResponse
{
    public List<MergeEntityResult> Results { get; set; }
}