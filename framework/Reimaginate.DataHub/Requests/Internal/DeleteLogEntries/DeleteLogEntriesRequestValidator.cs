using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.DeleteLogEntries;

public class DeleteLogEntriesRequestValidator : AbstractValidator<DeleteLogEntriesRequest>
{
    public DeleteLogEntriesRequestValidator()
    {
        RuleFor(r => r.Ids).NotEmpty();
    }
}