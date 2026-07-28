using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRegisterDuplicates;

public class ProcessRegisterDuplicatesRequestValidator : AbstractValidator<ProcessRegisterDuplicatesRequest>
{
    public ProcessRegisterDuplicatesRequestValidator()
    {
        RuleFor(r => r.Duplicates).NotEmpty();
    }
}