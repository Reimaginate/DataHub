using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.GetDataHubEntityByAltKey;

public class GetDataHubEntityByAltKeyRequestValidator : AbstractValidator<GetDataHubEntityByAltKeyRequest>
{
    public GetDataHubEntityByAltKeyRequestValidator()
    {
        RuleFor(r => r.Key).NotEmpty();
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.Value).NotEmpty();
    }
}