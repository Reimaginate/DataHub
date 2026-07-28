using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RegisterMergeSuccessesRequest : DataHubClientRequest<NullResponse>
{
    public RegisterMergeSuccessesRequest()
    {
        RequestType = nameof(RegisterMergeSuccessesRequest);
    }

    public List<MergeSuccess> MergeSuccesses { get; set; } = new();
}