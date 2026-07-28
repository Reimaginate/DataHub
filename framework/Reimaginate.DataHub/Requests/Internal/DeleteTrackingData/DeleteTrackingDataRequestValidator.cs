using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.DeleteTrackingData;

public class DeleteTrackingDataRequestValidator : AbstractValidator<DeleteTrackingDataRequest>
{
    public DeleteTrackingDataRequestValidator()
    {
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}