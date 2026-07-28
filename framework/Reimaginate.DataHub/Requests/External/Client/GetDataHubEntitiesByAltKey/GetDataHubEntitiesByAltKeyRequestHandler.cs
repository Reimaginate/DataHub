using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Requests.Internal.FindEntitiesByAlternateKey;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetDataHubEntitiesByAltKey;

public class GetDataHubEntitiesByAltKeyRequestHandler(IMediator mediator) : IHandler<GetDataHubEntitiesByAltKeyRequest, GetDataHubEntitiesByAltKeyResponse>
{
    public async Task<GetDataHubEntitiesByAltKeyResponse> HandleAsync(GetDataHubEntitiesByAltKeyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var results = new ConcurrentBag<JObject>();

            await Parallel.ForEachAsync(request.AlternateKeys, new ParallelOptions()
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, async (altKey, ct) =>
            {
                var findEntitiesByAlkKeyResponse = (await mediator.TrySend(new FindEntitiesByAlternateKeyRequest()
                {
                    EntityType = request.EntityType,
                    Key = altKey.Key,
                    Value = altKey.Value

                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                foreach (var entity in findEntitiesByAlkKeyResponse.Children().OfType<JObject>())
                {
                    results.Add(entity);
                }
            });


            return new GetDataHubEntitiesByAltKeyResponse()
            {
                Success = true,
                Results = results.ToList()
            };
        }
        catch (Exception ex)
        {
            return new GetDataHubEntitiesByAltKeyResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}