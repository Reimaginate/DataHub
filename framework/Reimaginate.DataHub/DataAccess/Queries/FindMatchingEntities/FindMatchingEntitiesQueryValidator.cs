using FluentValidation;

namespace Reimaginate.DataHub.DataAccess.Queries.FindMatchingEntities;

public sealed class FindMatchingEntitiesQueryValidator : AbstractValidator<FindMatchingEntitiesQuery>
{
    public FindMatchingEntitiesQueryValidator()
    {
        RuleFor(x => x.EntityType).NotEmpty();
        RuleFor(x => x.WhereClause).NotEmpty();
    }
}