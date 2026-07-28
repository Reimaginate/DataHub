using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.UpdateEntity;

public class UpdateEntityRequestValidator : AbstractValidator<UpdateEntityRequest>
{
    public UpdateEntityRequestValidator()
    {
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityId).NotEmpty();
        RuleFor(r => r.Data).NotNull();
    }
}