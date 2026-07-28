using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;

public class GetAllTrackingEntriesForEntitiesResponse
{
    public List<ChangeTrackingEntry> TrackingEntries { get; set; }
}