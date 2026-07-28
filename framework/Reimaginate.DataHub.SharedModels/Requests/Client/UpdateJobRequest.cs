using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateJobRequest : DataHubClientRequest<UpdateJobResponse>
{
    public UpdateJobRequest()
    {
        RequestType = nameof(UpdateJobRequest);
    }
    public JobDTO Job { get; set; }
}