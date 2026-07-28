using FluentValidation;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;



namespace Reimaginate.DataHub.Requests.Internal.LogSyncEvents;

public class LogSyncEventsRequestValidator : AbstractValidator<LogSyncEventsRequest<SyncEvent>>
{
    public LogSyncEventsRequestValidator()
    {
        RuleFor(r => r.SyncEvents).NotEmpty();
    }
}