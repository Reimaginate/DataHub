using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class SubmitJobsRequest : DataHubCLIRequest<SubmitJobsResponse>
{
    public SubmitJobsRequest()
    {
        RequestType = nameof(SubmitJobsRequest);
    }
    public List<JobDTO> Jobs { get; set; }
    public bool DisableNotifications { get; set; }
}