using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessDeleteDuplicates;

public class ProcessDeleteDuplicatesRequestValidator : AbstractValidator<ProcessDeleteDuplicatesRequest>
{
    public ProcessDeleteDuplicatesRequestValidator()
    {
        RuleFor(r => r.Where).NotEmpty();
    }
}