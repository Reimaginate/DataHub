using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.MergeExistingEntities;

public class MergeExistingEntitiesResponse
{
    public List<MergeEntityResult> Results { get; set; }
}