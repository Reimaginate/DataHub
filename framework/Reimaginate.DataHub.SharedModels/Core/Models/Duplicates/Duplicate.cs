using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;

public class Duplicate : ManagementEntry
{
    public Duplicate()
    {
        _dt = nameof(Duplicate);
    }

    public string Name { get; set; }
    public UserRef CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
    public DuplicateMergePlan MergePlan { get; set; }
    public string Status { get; set; }

    public string FailureReason { get; set; }
}