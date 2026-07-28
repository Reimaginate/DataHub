using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;

using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mapper;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetMergeFailuresWhere;

public class GetMergeFailuresWhereRequestHandler(IMediator mediator, IMapper mapper) : IHandler<GetMergeFailuresWhereRequest, GetMergeFailuresResponse>
{
    public async Task<GetMergeFailuresResponse> HandleAsync(GetMergeFailuresWhereRequest request, CancellationToken cancellationToken)
    {
        var whereClauses = new List<string>
        {
            "x.Type = @type"
        };

        if (!string.IsNullOrEmpty(request.WhereClause))
        {
            whereClauses.Add(LogEntryWhereClauseTranslator.TranslateEventAliases<MergeFailure>(request.WhereClause));
        }

        var where = string.Join(" and ", whereClauses);

        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<LogEntry>()
        {
            Select = request.Select,
            ContinuationToken = request.ContinuationToken,
            WhereClause = where,
            PageSize = request.PageSize,
            OrderBy = request.OrderBy,
            GetTotalResultCount = request.GetTotalResultCount,
            Parameters = DataHubQueryParameterMapper.Combine(
                [new QueryParameter("type", nameof(MergeFailure))],
                DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters))
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        
        var dtos = await mapper.MapAsync<List<MergeFailureDTO>>(response.Results, cancellationToken);

        return new GetMergeFailuresResponse()
        {
            Results = dtos,
            ContinuationToken = response.ContinuationToken,
            MoreResultsAvailable = response.MoreResultsAvailable,
            ResultCount = response.ResultCount
        };
    }
}
