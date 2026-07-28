using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RegisterDuplicatesRequest : DataHubClientRequest<RegisterDuplicatesResponse>
{
    public RegisterDuplicatesRequest()
    {
        RequestType = nameof(RegisterDuplicatesRequest);
    }
    public List<Duplicate> Duplicates { get; set; }
    public bool AutoSubmitMergeJobs { get; set; } = true;
}