using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;
using ClientPatchEntityRequest = Reimaginate.DataHub.SharedModels.Requests.Client.PatchEntityRequest;
using ClientPatchEntityResponse = Reimaginate.DataHub.SharedModels.Requests.Client.PatchEntityResponse;

namespace Reimaginate.DataHub.Requests.External.CLI.PatchDataHubEntitiesWhere;

public class PatchDataHubEntitiesWhereRequestHandler(IMediator mediator) : IHandler<PatchDataHubEntitiesWhereRequest, PatchDataHubEntitiesWhereResponse>
{
    public async Task<PatchDataHubEntitiesWhereResponse> HandleAsync(PatchDataHubEntitiesWhereRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var getDataHubEntitiesResponse = (await mediator.TrySend(new GetEntitiesWhereRequest
            {
                ContinuationToken = request.ContinuationToken,
                CorrelationId = request.CorrelationId,
                GetTotalResultCount = true,
                OrderBy = request.OrderBy,
                PageSize = request.PageSize,
                Select = request.Select,
                WhereClause = $"x.entityType = @entityType and ({request.Where})",
                Parameters = [
                    new DataHubQueryParameter { Name = "entityType", Value = request.EntityType },
                    ..request.Parameters ?? []
                ],
                User = request.User
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var results = new ConcurrentBag<ClientPatchEntityResponse>();
            var patchRequests = getDataHubEntitiesResponse.Results.Select(entity => new ClientPatchEntityRequest
            {
                CorrelationId = request.CorrelationId,
                DataSource = DataSources.DataHub,
                EntityType = request.EntityType,
                EntityId = entity.Value<string>(nameof(DataHubEntity.id)),
                Timestamp = request.Timestamp,
                Operations = request.Operations,
                DispatchNotifications = request.DispatchNotifications,
                Silent = request.Silent
            }).ToList();

            await Parallel.ForEachAsync(patchRequests, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, async (item, ct) =>
            {
                var patchEntityResponse = (await mediator.TrySend(item, ct)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                results.Add(patchEntityResponse);
            });

            return new PatchDataHubEntitiesWhereResponse
            {
                Success = true,
                Results = results.ToList(),
                ContinuationToken = getDataHubEntitiesResponse.ContinuationToken,
                MoreResultsAvailable = getDataHubEntitiesResponse.MoreResultsAvailable,
                ResultCount = getDataHubEntitiesResponse.ResultCount
            };
        }
        catch (Exception ex)
        {
            return new PatchDataHubEntitiesWhereResponse
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
