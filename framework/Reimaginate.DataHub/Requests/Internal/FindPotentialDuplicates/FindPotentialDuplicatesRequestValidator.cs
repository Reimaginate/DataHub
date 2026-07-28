using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.FindPotentialDuplicates;

public class FindPotentialDuplicatesRequestValidator : AbstractValidator<FindPotentialDuplicatesRequest>
{
    public FindPotentialDuplicatesRequestValidator()
    {
        RuleFor(r => r.DuplicatePreventionRule).NotNull();
        RuleFor(r => r.DataHubEntityType).NotEmpty();
        RuleFor(r => r.EntitiesToMatch).NotEmpty();
    }
}