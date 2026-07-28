using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetDuplicateRequest : DataHubCLIRequest<GetDuplicateResponse>
{
    public GetDuplicateRequest()
    {
        RequestType = nameof(GetDuplicateRequest);
    }
    public string Id { get; set; }
}