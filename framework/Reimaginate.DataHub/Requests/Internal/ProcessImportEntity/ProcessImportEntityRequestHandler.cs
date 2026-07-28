using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.CreateDataHubEntities;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;

namespace Reimaginate.DataHub.Requests.Internal.ProcessImportEntity;

public class ProcessImportEntityRequestHandler(IMediator mediator, IProcessingLockService processingLockService, ITimeService timeService)
    : IHandler<ProcessImportEntityRequest, ImportEntityResponse>
{
    public async Task<ImportEntityResponse> HandleAsync(ProcessImportEntityRequest request, CancellationToken cancellationToken)
    {
        var entityLockId = $"entities/{DataSources.DataHub}/{request.EntityType}/{request.EntityId}";
        ProcessingLock dataHubEntityLock = null;

        try
        {
            var getLockResponse = await processingLockService.WaitForLockAsync(entityLockId, request.CorrelationId, duration: TimeSpan.FromMinutes(5), waitTimeOut: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
            getLockResponse.ThrowIfUnsuccessful();
            dataHubEntityLock = getLockResponse.Result;

            var dataHubEntity = request.Data.RemoveNullValues();

            var now = timeService.Now();

            if (!dataHubEntity.ContainsKey(nameof(DataHubEntity.createdOn)))
                dataHubEntity.Add(nameof(DataHubEntity.createdOn), now);

            if (!dataHubEntity.ContainsKey(nameof(DataHubEntity.lastUpdated)))
                dataHubEntity.Add(nameof(DataHubEntity.lastUpdated), now);


            var tasks = new List<Task>();

            if (!request.Untracked.GetValueOrDefault(false))
            {
                tasks.Add(Task.Run(async () => (await mediator.SendAsync(new InitTrackedEntityRequest()
                {
                    DataSource = DataSources.DataHub,
                    EntityType = dataHubEntity.Value<string>(nameof(DataHubEntity.entityType)),
                    EntityId = dataHubEntity.Value<string>(nameof(DataHubEntity.id)),
                    EntityData = dataHubEntity,
                    Timestamp = dataHubEntity.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated))
                }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue }, cancellationToken));
            }
            
            tasks.Add(Task.Run(async () =>
            {
                _ = (await mediator.TrySend(new UpsertDataHubEntitiesCommand()
                {
                    Entities = [dataHubEntity]
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            }, cancellationToken));
            
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                return new ImportEntityResponse()
                {
                    EntityType = request.EntityType,
                    EntityId = request.EntityId,
                    Success = false,
                    FailureReason = ex.Message
                };
            }

            return new ImportEntityResponse()
            {
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                Success = true
            };
        }
        finally
        {
            if (dataHubEntityLock != null) await processingLockService.ReleaseLockAsync(dataHubEntityLock, cancellationToken);
        }
    }
}
