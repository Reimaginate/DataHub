using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdatedEntities;

public class ProcessUpdatedEntitiesResponse
{
    public List<MergeEntityResult> Results { get; set; } = new();
}