using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessNewEntityIntoExistingEntity;

public class ProcessNewEntityIntoExistingEntityRequestValidator : AbstractValidator<ProcessNewEntityIntoExistingEntityRequest>
{
    public ProcessNewEntityIntoExistingEntityRequestValidator()
    {
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.SourceEntityId).NotEmpty();

        RuleFor(r => r.FromEntity).NotNull();
        RuleFor(r => r.ToEntity).NotNull();

        RuleFor(r => r.EntityConfig).NotNull();
    }
}