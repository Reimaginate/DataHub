using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.GetDataHubEntitiesByAltKey;

public class GetDataHubEntitiesByAltKeyRequestValidator : AbstractValidator<GetDataHubEntitiesByAltKeyRequest>
{
    public GetDataHubEntitiesByAltKeyRequestValidator()
    {
        RuleFor(x => x.AlternateKeys).NotEmpty();
    }
}