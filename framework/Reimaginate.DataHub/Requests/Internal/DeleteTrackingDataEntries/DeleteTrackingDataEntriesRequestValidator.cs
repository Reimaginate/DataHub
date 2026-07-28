using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.DeleteTrackingDataEntries;

public class DeleteTrackingDataEntriesRequestValidator : AbstractValidator<DeleteTrackingDataEntriesRequest>
{
    public DeleteTrackingDataEntriesRequestValidator()
    {
        RuleFor(r => r.Ids).NotEmpty();
    }
}