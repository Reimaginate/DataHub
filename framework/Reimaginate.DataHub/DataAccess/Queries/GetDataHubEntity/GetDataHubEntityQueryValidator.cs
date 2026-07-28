using FluentValidation;

namespace Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntity;

public class GetDataHubEntityQueryValidator : AbstractValidator<GetDataHubEntityQuery>
{
    public GetDataHubEntityQueryValidator()
    {
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.Id).NotEmpty();
    }

}