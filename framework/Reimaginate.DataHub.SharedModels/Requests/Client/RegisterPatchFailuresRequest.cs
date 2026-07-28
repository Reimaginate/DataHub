using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RegisterPatchFailuresRequest : DataHubClientRequest<NullResponse>
{
    public RegisterPatchFailuresRequest()
    {
        RequestType = nameof(RegisterPatchFailuresRequest);
    }

    public List<PatchFailure> PatchFailures { get; set; } = new();
}