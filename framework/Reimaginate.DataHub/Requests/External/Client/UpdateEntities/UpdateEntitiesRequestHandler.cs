using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdateEntities;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;
using UpdateEntityResponse = Reimaginate.DataHub.SharedModels.Requests.Client.UpdateEntityResponse;

namespace Reimaginate.DataHub.Requests.External.Client.UpdateEntities;

public class UpdateEntitiesRequestHandler(IMediator mediator) : IHandler<UpdateEntitiesRequest, UpdateEntitiesResponse>
{
    public async Task<UpdateEntitiesResponse> HandleAsync(UpdateEntitiesRequest request, CancellationToken cancellationToken)
    {
        var processUpdateEntitiesRequest = new ProcessUpdateEntitiesRequest()
        {
            Requests = request.Requests.Select(s => new ProcessUpdateEntityRequest()
            {
                CreateOnly = false,
                Data = WithEntityIdentity(s.Data, s.EntityId, s.EntityType),
                DataSource = s.DataSource,
                EntityId = s.EntityId,
                EntityType = s.EntityType,
                ReturnResultingEntity = s.ReturnResultingEntity,
                Timestamp = s.Timestamp,
                UpdateOnly = !s.CreateIfMissing,
                UpdateType = s.UpdateType
            }).ToList()
        };

        var processUpdateEntitiesResponse = (await mediator.TrySend(processUpdateEntitiesRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        var failures = processUpdateEntitiesResponse.Results.Where(w => !w.Success).ToList();

        return new UpdateEntitiesResponse()
        {
            Success = processUpdateEntitiesResponse.Results.All(a => a.Success),
            FailureReason = failures.Any() ? $"ONE_OR_MORE_UPDATES_FAILED: {string.Join("\n", failures.Select(f => $"{f.EntityId}: {f.Error}"))}" : null,
            Results = processUpdateEntitiesResponse.Results.Select(s => new UpdateEntityResponse()
            {
                DataSource = s.DataSource,
                EntityId = s.EntityId,
                EntityType = s.EntityType,
                FailureReason = s.Error,
                ResultingEntity = s.ResultingEntity,
                Success = s.Success
            }).ToList()
        };
    }

    private static JObject WithEntityIdentity(JObject data, string entityId, string entityType)
    {
        var ret = (JObject)data.DeepClone();
        ret[nameof(DataHubEntity.id)] = entityId;
        ret[nameof(DataHubEntity.entityType)] ??= entityType;
        return ret;
    }
}
