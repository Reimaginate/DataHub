using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateDuplicates;

public class ProcessUpdateDuplicatesRequestValidator : AbstractValidator<ProcessUpdateDuplicatesRequest>
{
    public ProcessUpdateDuplicatesRequestValidator()
    {
        RuleFor(r => r.Duplicates).NotEmpty();
    }
}