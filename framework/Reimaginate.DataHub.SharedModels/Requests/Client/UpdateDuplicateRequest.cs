using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateDuplicateRequest : DataHubClientRequest<UpdateDuplicateResponse>
{
    public UpdateDuplicateRequest()
    {
        RequestType = nameof(UpdateDuplicateRequest);
    }
    public Duplicate Duplicate { get; set; }
}