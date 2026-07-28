using FluentValidation;

// ReSharper disable IdentifierTypo

namespace Reimaginate.DataHub.DataAccess.Commands.CreateDataHubEntities;

public class CreateDataHubEntitiesCommandValidator : AbstractValidator<CreateDataHubEntitiesCommand>
{
    public CreateDataHubEntitiesCommandValidator()
    {
        RuleFor(x => x.Entities).NotEmpty();
    }
}