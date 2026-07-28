using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RegisterDuplicateRequest : DataHubCLIRequest<RegisterDuplicateResponse>
{
    public RegisterDuplicateRequest()
    {
        RequestType = nameof(RegisterDuplicateRequest);
    }
    public Duplicate Duplicate { get; set; }
    public bool AutoSubmitMergeJobs { get; set; } = true;
}