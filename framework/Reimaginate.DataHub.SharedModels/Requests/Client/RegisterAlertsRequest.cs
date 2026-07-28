using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RegisterAlertsRequest : DataHubClientRequest<NullResponse>
{
    public RegisterAlertsRequest()
    {
        RequestType = nameof(RegisterAlertsRequest);
    }

    public List<Alert> Alerts { get; set; } = new();
}