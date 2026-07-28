using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.DataServices.Responses;

namespace Reimaginate.DataHub.Requests.Internal.ProcessGetDuplicates;

public class ProcessGetDuplicatesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public PagedResults<Duplicate> PagedResults { get; set; }

}