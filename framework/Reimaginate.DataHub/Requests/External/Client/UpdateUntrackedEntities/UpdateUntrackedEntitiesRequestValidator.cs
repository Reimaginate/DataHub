using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.UpdateUntrackedEntities;

public class UpdateUntrackedEntitiesRequestValidator : AbstractValidator<UpdateUntrackedEntitiesRequest>
{
    public UpdateUntrackedEntitiesRequestValidator()
    {
        RuleFor(r => r.Requests).NotEmpty();
        RuleForEach(r => r.Requests).ChildRules(request =>
        {
            request.RuleFor(r => r.EntityType).NotEmpty();
            request.RuleFor(r => r.EntityId).NotEmpty();
            request.RuleFor(r => r.Data).NotNull();
        });
    }
}
