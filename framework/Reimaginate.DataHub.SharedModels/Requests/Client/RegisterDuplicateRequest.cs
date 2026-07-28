using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RegisterDuplicateRequest : DataHubClientRequest<RegisterDuplicateResponse>
{
    public RegisterDuplicateRequest()
    {
        RequestType = nameof(RegisterDuplicateRequest);
    }
    public Duplicate Duplicate { get; set; }
    public bool AutoSubmitMergeJobs { get; set; } = true;
}