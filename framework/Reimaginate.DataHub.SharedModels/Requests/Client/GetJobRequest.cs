
namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetJobRequest : DataHubClientRequest<GetJobResponse>
{
    public GetJobRequest()
    {
        RequestType = nameof(GetJobRequest);
    }
    public string JobId { get; set; }
}