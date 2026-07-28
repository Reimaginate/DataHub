using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class UpdateJobsRequest : DataHubCLIRequest<UpdateJobsResponse>
{
    public UpdateJobsRequest()
    {
        RequestType = nameof(UpdateJobsRequest);
    }
    public List<JobDTO> Jobs { get; set; }
}