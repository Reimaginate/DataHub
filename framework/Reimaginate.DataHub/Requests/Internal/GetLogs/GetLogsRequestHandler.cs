using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;

using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.GetLogs;

public class GetLogsRequestHandler<TSyncEvent>(IMediator mediator) : IHandler<GetLogsRequest<TSyncEvent>, GetLogsResponse>
    where TSyncEvent : SyncEvent
{
    public async Task<GetLogsResponse> HandleAsync(GetLogsRequest<TSyncEvent> request, CancellationToken cancellationToken)
    {
        var whereClauses = new List<string>() { "x.Type = @type" };
        if (!string.IsNullOrEmpty(request.WhereClause)) whereClauses.Add(request.WhereClause);
        var where = string.Join(" and ", whereClauses);
        var parameters = DataHubQueryParameterMapper.Combine(
            [new QueryParameter("type", typeof(TSyncEvent).Name)],
            request.Parameters);

        var results = new List<LogEntry>();

        var queryResponse = (await mediator.TrySend(new GetCosmosDocumentsQuery<LogEntry>()
        {
            ContinuationToken = request.ContinuationToken,
            WhereClause = where,
            PageSize = request.PageSize,
            Parameters = parameters
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        
        results.AddRange(queryResponse.Results);
        
        if (request.RetrieveAllResults)
        {
            while (queryResponse.MoreResultsAvailable)
            {
                queryResponse = (await mediator.TrySend(new GetCosmosDocumentsQuery<LogEntry>()
                {
                    ContinuationToken = queryResponse.ContinuationToken,
                    WhereClause = where,
                    PageSize = request.PageSize,
                    Parameters = parameters
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                results.AddRange(queryResponse.Results);
            }
        }

        return new GetLogsResponse()
        {
            Results = results,
            ContinuationToken = queryResponse.ContinuationToken,
            MoreResultsAvailable = queryResponse.MoreResultsAvailable,
            ResultCount = queryResponse.ResultCount
        };
    }
}
