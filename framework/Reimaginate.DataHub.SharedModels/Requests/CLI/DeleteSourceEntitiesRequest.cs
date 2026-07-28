using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteSourceEntitiesRequest : DataHubCLIRequest<DeleteSourceEntitiesResponse>
{
    public DeleteSourceEntitiesRequest()
    {
        RequestType = nameof(DeleteSourceEntitiesRequest);
    }

    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
}