using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessGetDuplicates;

public class ProcessGetDuplicatesRequestValidator : AbstractValidator<ProcessGetDuplicatesRequest>
{
    public ProcessGetDuplicatesRequestValidator()
    {
        //RuleFor(r => r.).NotEmpty();
    }
}