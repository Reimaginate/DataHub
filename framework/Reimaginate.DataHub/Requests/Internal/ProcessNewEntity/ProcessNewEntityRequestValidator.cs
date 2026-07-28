using FluentValidation;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.ProcessNewEntity;

public class ProcessNewEntityRequestValidator : AbstractValidator<ProcessNewEntityRequest>
{
    public ProcessNewEntityRequestValidator()
    {
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.SourceEntityType).NotEmpty();
        RuleFor(r => r.SourceEntityId).NotEmpty();
        RuleFor(r => r.SourceEntity).NotNull();
        RuleFor(r => r.SourceEntity.ContainsKey(nameof(DataHubEntity.createdOn)) && r.SourceEntity[nameof(DataHubEntity.createdOn)].Type != JTokenType.Null).Equal(true).WithMessage("Entity.createdOn must not be null");
    }
}