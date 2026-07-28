using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateDuplicatesRequest : DataHubClientRequest<UpdateDuplicatesResponse>
{
    public UpdateDuplicatesRequest()
    {
        RequestType = nameof(UpdateDuplicatesRequest);
    }
    public List<Duplicate> Duplicates { get; set; }
}