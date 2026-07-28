using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.PatchDataHubEntitiesWhere;

public class PatchDataHubEntitiesWhereRequestHandler(IMediator mediator) : IHandler<PatchDataHubEntitiesWhereRequest, PatchDataHubEntitiesWhereResponse>
{
    public async Task<PatchDataHubEntitiesWhereResponse> HandleAsync(PatchDataHubEntitiesWhereRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var results = new ConcurrentBag<PatchEntityResponse>();

            var getDataHubEntitiesResponse = (await mediator.TrySend(new GetDataHubEntitiesWhereRequest()
            {
                ContinuationToken = request.ContinuationToken,
                CorrelationId = request.CorrelationId,
                GetTotalResultCount = true,
                OrderBy = request.OrderBy,
                PageSize = request.PageSize,
                Select = request.Select,
                From = request.From,
                WhereClause = $"x.entityType = @entityType and ({request.Where})",
                Parameters = [
                    new DataHubQueryParameter { Name = "entityType", Value = request.EntityType },
                    ..request.Parameters ?? []
                ]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var patchRequests = getDataHubEntitiesResponse.Results.Select(s => new PatchEntityRequest()
            {
                CorrelationId = request.CorrelationId,
                DataSource = DataSources.DataHub,
                EntityType = request.EntityType,
                EntityId = s.Value<string>(nameof(DataHubEntity.id)),
                Timestamp = request.Timestamp,
                Operations = request.Operations,
                DispatchNotifications = request.DispatchNotifications,
                Silent = request.Silent
            }).ToList();


            await Parallel.ForEachAsync(patchRequests, new ParallelOptions()
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, async (item, ct) =>
            {
                var patchEntityResponse = (await mediator.TrySend(item, ct)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                results.Add(patchEntityResponse);
            });

            return new PatchDataHubEntitiesWhereResponse()
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
            return new PatchDataHubEntitiesWhereResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
