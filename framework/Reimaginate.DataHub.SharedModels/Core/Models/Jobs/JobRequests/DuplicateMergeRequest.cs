using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;

namespace Reimaginate.DataHub.SharedModels.Core.Models.Jobs.JobRequests;

public class DuplicateMergeRequest : JobRequest
{
    public DuplicateMergeRequest()
    {
        RequestType = nameof(DuplicateMergeRequest);
    }
    public string DuplicateId { get; set; }
    public DuplicateMergePlan MergePlan { get; set; }
}