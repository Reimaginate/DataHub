using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RegisterSyncSuccessesRequest : DataHubClientRequest<NullResponse>
{
    public RegisterSyncSuccessesRequest()
    {
        RequestType = nameof(RegisterSyncSuccessesRequest);
    }

    public List<SyncSuccess> SyncSuccesses { get; set; } = new();
}