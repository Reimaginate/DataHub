using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Requests.Internal.DetachEntitiesFromDataSource;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;
using InternalDetachEntitiesFromDataSourceRequest = Reimaginate.DataHub.Requests.Internal.DetachEntitiesFromDataSource.DetachEntitiesFromDataSourceRequest;
using InternalDetachEntitiesFromDataSourceResponse = Reimaginate.DataHub.Requests.Internal.DetachEntitiesFromDataSource.DetachEntitiesFromDataSourceResponse;

namespace Reimaginate.DataHub.Requests.External.CLI.DetachEntities;

public class DetachEntitiesRequestHandler(IMediator mediator, IProcessingLockService processingLockService)
    : IHandler<DetachEntitiesRequest, DetachEntitiesResponse>
{
    public async Task<DetachEntitiesResponse> HandleAsync(DetachEntitiesRequest request, CancellationToken cancellationToken)
    {
        var entityLockIds = request.EntityIds.Select(entityId => $"entities/{DataSources.DataHub}/{request.EntityType}/{entityId}").ToList();
        List<ProcessingLock> dataHubEntityLocks = null;
        try
        {

            var getLocksResponse = await processingLockService.WaitForLocksAsync(entityLockIds, request.CorrelationId, duration: TimeSpan.FromMinutes(5), waitTimeOut: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
            getLocksResponse.ThrowIfUnsuccessful();
            dataHubEntityLocks = getLocksResponse.Result;

            var detachEntitiesFromDataSourceResponse = (await mediator.TrySend(new InternalDetachEntitiesFromDataSourceRequest()
            {
                EntityType = request.EntityType,
                EntityIds = request.EntityIds,
                DataSource = request.DataSource
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var getDataHubEntitiesByIdResponse = (await mediator.TrySend(new GetDataHubEntitiesByIdRequest()
            {
                EntityType = request.EntityType,
                EntityIds = request.EntityIds
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            return new DetachEntitiesResponse()
            {
                Success = !detachEntitiesFromDataSourceResponse.Failures.Any(),
                FailureReason = detachEntitiesFromDataSourceResponse.Failures.Any()
                    ? $"{detachEntitiesFromDataSourceResponse.Failures.Count} detach operation(s) failed."
                    : null,
                ResultingEntities = getDataHubEntitiesByIdResponse.Results,
                Failures = detachEntitiesFromDataSourceResponse.Failures
            };
        }
        finally
        {
            if (dataHubEntityLocks != null) await processingLockService.ReleaseLocksAsync(dataHubEntityLocks, cancellationToken);
        }
    }
}
