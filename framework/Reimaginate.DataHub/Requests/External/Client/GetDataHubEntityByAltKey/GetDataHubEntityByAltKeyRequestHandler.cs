using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Requests.Internal.FindEntitiesByAlternateKey;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetDataHubEntityByAltKey;

public class GetDataHubEntityByAltKeyRequestHandler(IMediator mediator) : IHandler<GetDataHubEntityByAltKeyRequest, GetDataHubEntityByAltKeyResponse>
{
    public async Task<GetDataHubEntityByAltKeyResponse> HandleAsync(GetDataHubEntityByAltKeyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var findEntitiesByAlkKeyResponse = (await mediator.TrySend(new FindEntitiesByAlternateKeyRequest()
            {
                EntityType = request.EntityType,
                Key = request.Key,
                Value = request.Value

            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var results = findEntitiesByAlkKeyResponse.OfType<JObject>().ToList();

            if (results.Count > 1)
            {
                return new GetDataHubEntityByAltKeyResponse()
                {
                    Success = false,
                    FailureReason = "Multiple entities found with matching alternate keys"
                };

            }

            return new GetDataHubEntityByAltKeyResponse()
            {
                Success = true,
                Result = findEntitiesByAlkKeyResponse.OfType<JObject>().FirstOrDefault()
            };
        }
        catch (Exception ex)
        {
            return new GetDataHubEntityByAltKeyResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}