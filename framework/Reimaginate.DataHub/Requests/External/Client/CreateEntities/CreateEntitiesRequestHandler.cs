using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdateEntities;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.CreateEntities;

public class CreateEntitiesRequestHandler(IMediator mediator) : IHandler<CreateEntitiesRequest, CreateEntitiesResponse>
{
    public async Task<CreateEntitiesResponse> HandleAsync(CreateEntitiesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var processUpdateEntitiesRequest = new ProcessUpdateEntitiesRequest()
            {
                Requests = request.Requests.Select(s => new ProcessUpdateEntityRequest()
                {
                    UpdateOnly = false,
                    Data = WithEntityIdentity(s.Data, s.EntityId, s.EntityType),
                    DataSource = s.DataSource,
                    EntityId = s.EntityId,
                    EntityType = s.EntityType,
                    ReturnResultingEntity = s.ReturnResultingEntity,
                    Timestamp = s.Timestamp,
                    CreateOnly = true,
                    UpdateType = null

                }).ToList()
            };

            var processUpdateEntitiesResponse = (await mediator.TrySend(processUpdateEntitiesRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            return new CreateEntitiesResponse()
            {
                Success = true,
                Results = processUpdateEntitiesResponse.Results.Select(s => new CreateEntityResponse()
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
        catch (Exception ex)
        {
            return new CreateEntitiesResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }

    private static JObject WithEntityIdentity(JObject data, string entityId, string entityType)
    {
        var ret = (JObject)data.DeepClone();
        ret[nameof(DataHubEntity.id)] = entityId;
        ret[nameof(DataHubEntity.entityType)] ??= entityType;
        return ret;
    }
}
