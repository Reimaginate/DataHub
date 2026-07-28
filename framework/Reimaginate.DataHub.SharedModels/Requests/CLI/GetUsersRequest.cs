using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetUsersRequest: DataHubCLIRequest<GetUsersResponse>
{
    public GetUsersRequest()
    {
        RequestType = nameof(GetUsersRequest);
    }
    public string Where { get; set; }
    public string ContinuationToken { get; set; }
}