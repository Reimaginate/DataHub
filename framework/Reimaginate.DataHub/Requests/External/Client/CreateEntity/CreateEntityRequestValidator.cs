using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.CreateEntity;

public class CreateEntityRequestValidator : AbstractValidator<CreateEntityRequest>
{
    public CreateEntityRequestValidator()
    {
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityId).NotEmpty();
        RuleFor(r => r.Data).NotNull();
    }
}