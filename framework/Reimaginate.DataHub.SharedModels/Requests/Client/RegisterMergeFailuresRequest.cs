using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RegisterMergeFailuresRequest : DataHubClientRequest<NullResponse>
{
    public RegisterMergeFailuresRequest()
    {
        RequestType = nameof(RegisterMergeFailuresRequest);
    }

    public List<MergeFailure> MergeFailures { get; set; } = new();
}