using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.Diagnostics;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.LogEvents;

public class LogEventsRequestHandler<TEvent>(IIdService idService, IMediator mediator) : IHandler<LogEventsRequest<TEvent>, LogEventsResponse>
    where TEvent : Event, new()
{
    public async Task<LogEventsResponse> HandleAsync(LogEventsRequest<TEvent> request, CancellationToken cancellationToken)
    {
        try
        {
            var newLogEntries = request.Events.Select(s => new LogEntry()
            {
                id = idService.NewId<LogEntry>(),
                Type = typeof(TEvent).Name,
                Data = JObject.FromObject(s),
                Timestamp = s.Timestamp
            }).ToList();

            var createResponse = (await mediator.TrySend(new CreateCosmosDocumentsCommand<LogEntry>()
            {
                Documents = newLogEntries
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (createResponse.Failures?.Any() == true)
            {
                return LogEventFailureHelpers.CreatePersistenceFailureResponse(createResponse.Failures);
            }

            RecordDomainEvents(typeof(TEvent).Name, newLogEntries.Count);

            return new LogEventsResponse()
            {
                Success = true,
                LogEntries = createResponse.Successes ?? newLogEntries
            };
        }
        catch (Exception ex)
        {
            return new LogEventsResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }

    private static void RecordDomainEvents(string eventType, long count)
    {
        if (eventType.EndsWith("Failure", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventType, nameof(Alert), StringComparison.OrdinalIgnoreCase))
        {
            DataHubTelemetry.RecordDomainFailure(eventType, "log_events", count);
        }
    }
}
