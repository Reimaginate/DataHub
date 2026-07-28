using System.Collections.Generic;
using System.Linq;
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

namespace Reimaginate.DataHub.Requests.External.CLI.GetPatchFailuresById;

public class GetPatchFailuresByIdRequestHandler(IMediator mediator, IMapper mapper) : IHandler<GetPatchFailuresByIdRequest, GetPatchFailuresResponse>
{
    public async Task<GetPatchFailuresResponse> HandleAsync(GetPatchFailuresByIdRequest request, CancellationToken cancellationToken)
    {
        var parameters = new List<QueryParameter>
        {
            new("type", nameof(PatchFailure))
        };
        var idParameterNames = DataHubQueryParameterMapper.AddIndexedParameters(request.Ids, "id", parameters);
        var where = $"x.Type = @type and x.id in ({string.Join(",", idParameterNames)})";
        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<LogEntry>
        {
            Select = request.Select,
            WhereClause = where,
            Parameters = parameters
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        var dtos = await mapper.MapAsync<List<PatchFailureDTO>>(response.Results, cancellationToken);
        return new GetPatchFailuresResponse
        {
            Results = dtos,
            ResultCount = dtos.Count,
            MoreResultsAvailable = false
        };
    }
}
