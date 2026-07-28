using System;
using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Core;

public class RebaseEntityTrackingResult
{
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public DateTimeOffset? RebasedTo { get; set; }
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<ChangeTrackingEntry> ArchivedTrackingEntries { get; set; }
}