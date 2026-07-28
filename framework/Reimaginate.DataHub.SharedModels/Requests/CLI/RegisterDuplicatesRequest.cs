using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RegisterDuplicatesRequest : DataHubCLIRequest<RegisterDuplicatesResponse>
{
    public RegisterDuplicatesRequest()
    {
        RequestType = nameof(RegisterDuplicatesRequest);
    }
    public List<Duplicate> Duplicates { get; set; }
    public bool AutoSubmitMergeJobs { get; set; } = true;
}