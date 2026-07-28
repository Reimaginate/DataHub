using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class UpdateDuplicateRequest : DataHubCLIRequest<UpdateDuplicateResponse>
{
    public UpdateDuplicateRequest()
    {
        RequestType = nameof(UpdateDuplicateRequest);
    }
    public Duplicate Duplicate { get; set; }
}