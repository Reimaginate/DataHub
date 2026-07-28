using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class UpdateDuplicatesRequest : DataHubCLIRequest<UpdateDuplicatesResponse>
{
    public UpdateDuplicatesRequest()
    {
        RequestType = nameof(UpdateDuplicatesRequest);
    }
    public List<Duplicate> Duplicates { get; set; }
}