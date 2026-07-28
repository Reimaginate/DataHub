using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RegisterSyncFailuresRequest : DataHubClientRequest<NullResponse>
{
    public RegisterSyncFailuresRequest()
    {
        RequestType = nameof(RegisterSyncFailuresRequest);
    }

    public List<SyncFailure> SyncFailures { get; set; } = new();
}