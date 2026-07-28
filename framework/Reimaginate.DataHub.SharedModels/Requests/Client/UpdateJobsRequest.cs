using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateJobsRequest : DataHubClientRequest<UpdateJobsResponse> 
{
    public UpdateJobsRequest()
    {
        RequestType = nameof(UpdateJobsRequest);
    }
    public List<JobDTO> Jobs { get; set; }
}