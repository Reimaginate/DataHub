using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.CheckPreMergeRules;

public class CheckPreMergeRulesRequestValidator : AbstractValidator<CheckPreMergeRulesRequest>
{
    public CheckPreMergeRulesRequestValidator()
    {
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.SourceEntityType).NotNull();
    }
}