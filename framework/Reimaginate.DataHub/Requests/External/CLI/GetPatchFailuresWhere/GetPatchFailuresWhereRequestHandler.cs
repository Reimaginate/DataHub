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

namespace Reimaginate.DataHub.Requests.External.CLI.GetPatchFailuresWhere;

public class GetPatchFailuresWhereRequestHandler(IMediator mediator, IMapper mapper) : IHandler<GetPatchFailuresWhereRequest, GetPatchFailuresResponse>
{
    public async Task<GetPatchFailuresResponse> HandleAsync(GetPatchFailuresWhereRequest request, CancellationToken cancellationToken)
    {
        var whereClauses = new List<string>
        {
            "x.Type = @type"
        };

        if (!string.IsNullOrEmpty(request.WhereClause))
        {
            whereClauses.Add(LogEntryWhereClauseTranslator.TranslateEventAliases<PatchFailure>(request.WhereClause));
        }

        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<LogEntry>
        {
            Select = request.Select,
            ContinuationToken = request.ContinuationToken,
            WhereClause = string.Join(" and ", whereClauses),
            PageSize = request.PageSize,
            OrderBy = request.OrderBy,
            GetTotalResultCount = request.GetTotalResultCount,
            Parameters = DataHubQueryParameterMapper.Combine(
                [new QueryParameter("type", nameof(PatchFailure))],
                DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters))
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        var dtos = await mapper.MapAsync<List<PatchFailureDTO>>(response.Results, cancellationToken);
        return new GetPatchFailuresResponse
        {
            Results = dtos,
            ContinuationToken = response.ContinuationToken,
            MoreResultsAvailable = response.MoreResultsAvailable,
            ResultCount = response.ResultCount
        };
    }
}
