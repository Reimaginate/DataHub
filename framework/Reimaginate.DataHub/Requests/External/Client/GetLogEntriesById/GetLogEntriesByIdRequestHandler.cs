using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetLogEntriesById;

public class GetLogEntriesByIdRequestHandler(IMediator mediator) : IHandler<GetLogEntriesByIdRequest, GetLogEntriesResponse>
{
    public async Task<GetLogEntriesResponse> HandleAsync(GetLogEntriesByIdRequest request, CancellationToken cancellationToken)
    {
        var parameters = new List<QueryParameter>();
        var idParameterNames = DataHubQueryParameterMapper.AddIndexedParameters(request.Ids, "id", parameters);
        var where = $"x.id in ({string.Join(",", idParameterNames)})";

        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<LogEntry>()
        {
            Select = request.Select,
            WhereClause = where,
            Parameters = parameters
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        
        return new GetLogEntriesResponse()
        {
            Success = true,
            Results = response.Results,
            ResultCount = response.ResultCount,
            MoreResultsAvailable = false
        };
    }
}
