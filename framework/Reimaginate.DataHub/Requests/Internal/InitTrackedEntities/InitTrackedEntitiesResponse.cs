using System.Collections.Generic;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.InitTrackedEntities;

public class InitTrackedEntitiesResponse
{
    public List<ChangeTrackingEntry> Successes { get; set; }

    public List<DataAccessFailure<ChangeTrackingEntry>> Failures { get; set; }
}