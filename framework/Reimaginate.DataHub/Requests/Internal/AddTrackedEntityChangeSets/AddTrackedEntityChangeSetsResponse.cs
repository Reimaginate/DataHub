using System.Collections.Generic;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;

public class AddTrackedEntityChangeSetsResponse
{
    public List<ChangeTrackingEntry> Successes { get; set; }
    public List<DataAccessFailure<ChangeTrackingEntry>> Failures { get; set; }
}