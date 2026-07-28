using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Queries.FindMatchingEntities;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.FindEntitiesByAlternateKey;

public class FindEntitiesByAlternateKeyRequestHandler(IMediator mediator) : IHandler<FindEntitiesByAlternateKeyRequest, JArray>
{
    public async Task<JArray> HandleAsync(FindEntitiesByAlternateKeyRequest request, CancellationToken cancellationToken)
    {
        var matchWhereClause = $"exists (select ak from ak in x.{nameof(DataHubEntity.alternateKeys)} where ak['{nameof(AlternateKey.Key)}'] = @alternateKey and ak['{nameof(AlternateKey.Value)}'] = @alternateValue)";
        var findMatchingEntitiesQuery = new FindMatchingEntitiesQuery()
        {
            EntityType = request.EntityType,
            WhereClause = matchWhereClause,
            Parameters =
            [
                new QueryParameter("alternateKey", request.Key),
                new QueryParameter("alternateValue", request.Value)
            ]
        };

        var ret = (await mediator.TrySend(findMatchingEntitiesQuery, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        return ret;
    }
}
