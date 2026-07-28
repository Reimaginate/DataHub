using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.DispatchNotifications;

public class DispatchNotificationsRequestValidator : AbstractValidator<DispatchNotificationsRequest>
{
    public DispatchNotificationsRequestValidator()
    {
        RuleFor(r => r.Notifications).NotEmpty();
    }

}