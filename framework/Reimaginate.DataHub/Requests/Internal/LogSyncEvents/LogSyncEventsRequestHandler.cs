using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.Diagnostics;
using Reimaginate.DataHub.Requests.Internal.GetLogs;
using Reimaginate.DataHub.Requests.Internal.LogEvents;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.DataServices;

using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.LogSyncEvents;

public class LogSyncEventsRequestHandler<TSyncEvent>(IIdService idService, IMediator mediator) : IHandler<LogSyncEventsRequest<TSyncEvent>, LogEventsResponse>
    where TSyncEvent : SyncEvent
{
    public async Task<LogEventsResponse> HandleAsync(LogSyncEventsRequest<TSyncEvent> request, CancellationToken cancellationToken)
    {
        try
        {
            #region Clear existing sync successes and failures for target entities

            var existingLogEntries = new List<LogEntry>();

            var groupedByDataSource = request.SyncEvents.GroupBy(g => g.DataSource);

            foreach (var dataSourceGroup in groupedByDataSource)
            {
                if (typeof(TSyncEvent) == typeof(SyncFailure) || typeof(TSyncEvent) == typeof(SyncSuccess))
                {
                    var dataHubEntities = dataSourceGroup.Where(w => !string.IsNullOrEmpty(w.DataHubEntityId)).Select(s => new { s.DataHubEntityType, s.DataHubEntityId }).ToList();

                    var getSyncFailuresResponse = (await mediator.TrySend(
                        CreateExistingLogQuery<SyncFailure>(dataSourceGroup.Key, dataHubEntities.Select(entity => entity.DataHubEntityId), nameof(SyncEvent.DataHubEntityId)),
                        cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    var getSyncSuccessesResponse = (await mediator.TrySend(
                        CreateExistingLogQuery<SyncSuccess>(dataSourceGroup.Key, dataHubEntities.Select(entity => entity.DataHubEntityId), nameof(SyncEvent.DataHubEntityId)),
                        cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    var allResults = getSyncFailuresResponse.Results.Concat(getSyncSuccessesResponse.Results).ToList();
                    existingLogEntries.AddRange(allResults);
                }

                if (typeof(TSyncEvent) == typeof(MergeFailure) || typeof(TSyncEvent) == typeof(MergeSuccess))
                {
                    var sourceEntities = dataSourceGroup.Where(w => !string.IsNullOrEmpty(w.SourceEntityId)).Select(s => new { s.DataSource, s.SourceEntityType, s.SourceEntityId }).ToList();

                    var getMergeFailuresResponse = (await mediator.TrySend(
                        CreateExistingLogQuery<MergeFailure>(dataSourceGroup.Key, sourceEntities.Select(entity => entity.SourceEntityId), nameof(SyncEvent.SourceEntityId)),
                        cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    var getMergeSuccessesResponse = (await mediator.TrySend(
                        CreateExistingLogQuery<MergeSuccess>(dataSourceGroup.Key, sourceEntities.Select(entity => entity.SourceEntityId), nameof(SyncEvent.SourceEntityId)),
                        cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    var allResults = getMergeFailuresResponse.Results.Concat(getMergeSuccessesResponse.Results).ToList();
                    existingLogEntries.AddRange(allResults);
                }
            }

            if (existingLogEntries.Any())
            {
                _ = (await mediator.TrySend(new DeleteCosmosDocumentsCommand<LogEntry>()
                {
                    Documents = existingLogEntries
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            }

            #endregion

            if (typeof(TSyncEvent) == typeof(MergeSuccess))
                return new LogEventsResponse()
                {
                    Success = true
                };
            if (typeof(TSyncEvent) == typeof(SyncSuccess))
                return new LogEventsResponse()
                {
                    Success = true
                };

            #region Write new log entries

            var newLogEntries = request.SyncEvents.Select(s => new LogEntry()
            {
                id = idService.NewId<LogEntry>(),
                Type = typeof(TSyncEvent).Name,
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

            DataHubTelemetry.RecordDomainFailure(typeof(TSyncEvent).Name, "log_sync_events", newLogEntries.Count);

            #endregion

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

    private static GetLogsRequest<TLogEvent> CreateExistingLogQuery<TLogEvent>(
        string dataSource,
        IEnumerable<string> entityIds,
        string entityIdPropertyName)
        where TLogEvent : SyncEvent
    {
        var parameters = new List<QueryParameter>
        {
            new("dataSource", dataSource)
        };
        var entityIdParameterNames = DataHubQueryParameterMapper.AddIndexedParameters(entityIds, "entityId", parameters);

        return new GetLogsRequest<TLogEvent>
        {
            WhereClause = $"x.Data.DataSource = @dataSource and x.Data.{entityIdPropertyName} in ({string.Join(",", entityIdParameterNames)})",
            OrderBy = "x.lastUpdated",
            RetrieveAllResults = true,
            Parameters = parameters
        };
    }
}
