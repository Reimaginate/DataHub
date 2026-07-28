using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessGetUsers;

public class ProcessGetUsersRequestValidator : AbstractValidator<ProcessGetUsersRequest>
{
    public ProcessGetUsersRequestValidator()
    {
        //RuleFor(r => r.).NotEmpty();
    }
}