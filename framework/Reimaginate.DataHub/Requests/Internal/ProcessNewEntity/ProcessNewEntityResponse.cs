using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.ProcessNewEntity;


public class ProcessNewEntityResponse
{
    public MergeEntityResult MergeEntityResult { get; set; }
    public List<ChangeTrackingEntry> ResultingChangeTrackingEntries { get; set; }
}