using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.GetDataHubEntity;

public class GetDataHubEntityRequestValidator : AbstractValidator<GetDataHubEntityRequest>
{
    public GetDataHubEntityRequestValidator()
    {
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityId).NotEmpty();
    }
}