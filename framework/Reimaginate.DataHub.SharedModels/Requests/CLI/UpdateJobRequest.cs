using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class UpdateJobRequest: DataHubCLIRequest<UpdateJobResponse> 
{
    public UpdateJobRequest()
    {
        RequestType = nameof(UpdateJobRequest);
    }
    public JobDTO Job { get; set; }
}