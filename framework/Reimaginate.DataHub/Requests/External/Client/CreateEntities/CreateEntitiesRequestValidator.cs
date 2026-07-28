using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.CreateEntities;

public class CreateEntitiesRequestValidator : AbstractValidator<CreateEntitiesRequest>
{
    public CreateEntitiesRequestValidator()
    {
        RuleFor(r => r.Requests).NotEmpty();
    }
}