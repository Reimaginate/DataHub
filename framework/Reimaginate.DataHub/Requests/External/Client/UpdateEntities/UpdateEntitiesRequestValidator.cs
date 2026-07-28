using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;


namespace Reimaginate.DataHub.Requests.External.Client.UpdateEntities;

public class UpdateEntitiesRequestValidator : AbstractValidator<UpdateEntitiesRequest>
{
    public UpdateEntitiesRequestValidator()
    {
        RuleFor(r => r.Requests).NotEmpty();
    }
}