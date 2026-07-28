using FluentValidation;

// ReSharper disable IdentifierTypo

namespace Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;

public class UpsertDataHubEntitiesCommandValidator : AbstractValidator<UpsertDataHubEntitiesCommand>
{
    public UpsertDataHubEntitiesCommandValidator()
    {
        RuleFor(x => x.Entities).NotEmpty();
    }
}