using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.MergeNewEntities;

public class MergeNewEntitiesResponse
{
    public List<MergeEntityResult> Successes { get; set; } = new();
    public List<MergeEntityResult> Failures { get; set; } = new();
}