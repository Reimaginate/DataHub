using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetLogEntriesWhere;

public class GetLogEntriesWhereRequestHandler(IMediator mediator) : IHandler<GetLogEntriesWhereRequest, GetLogEntriesResponse>
{
    public async Task<GetLogEntriesResponse> HandleAsync(GetLogEntriesWhereRequest request, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<LogEntry>()
        {
            Select = request.Select,
            ContinuationToken = request.ContinuationToken,
            WhereClause = request.WhereClause,
            PageSize = request.PageSize,
            OrderBy = request.OrderBy,
            GetTotalResultCount = request.GetTotalResultCount,
            Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
       
        return new GetLogEntriesResponse()
        {
            Success = true,
            Results = response.Results,
            ContinuationToken = response.ContinuationToken,
            MoreResultsAvailable = response.MoreResultsAvailable,
            ResultCount = response.ResultCount
        };
    }
}
