using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class SubmitJobsRequest : DataHubClientRequest<SubmitJobsResponse>
{
    public SubmitJobsRequest()
    {
        RequestType = nameof(SubmitJobsRequest);
    }

    public List<JobDTO> Jobs { get; set; }
    public bool DisableNotifications { get; set; }
}