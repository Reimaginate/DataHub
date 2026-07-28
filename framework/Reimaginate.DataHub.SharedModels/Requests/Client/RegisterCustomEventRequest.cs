using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RegisterCustomEventRequest : DataHubClientRequest<NullResponse>
{
    public RegisterCustomEventRequest()
    {
        RequestType = nameof(RegisterCustomEventRequest);
    }

    public List<CustomEvent> CustomEvents { get; set; } = new();
}