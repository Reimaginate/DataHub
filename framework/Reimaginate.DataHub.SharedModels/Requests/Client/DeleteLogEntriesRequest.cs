using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class DeleteLogEntriesRequest : DataHubClientRequest<DeleteLogEntriesResponse>
{
    public DeleteLogEntriesRequest()
    {
        RequestType = nameof(DeleteLogEntriesRequest);
    }

    public List<string> Ids { get; set; }
}