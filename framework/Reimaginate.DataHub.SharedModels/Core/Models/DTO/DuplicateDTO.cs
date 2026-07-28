using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using System;
using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Core.Models.DTO;

public class DuplicateDTO
{
    public string DuplicateId { get; set; }
    public string Name { get; set; }
    public UserRef CreatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset? LastUpdated { get; set; }
    public string LastUpdatedBy { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
    public DuplicateMergePlan MergePlan { get; set; }
    public string Status { get; set; }
    public string FailureReason { get; set; }

}