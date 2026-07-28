using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntities;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetUpdatedDataHubEntities;

public class GetUpdatedDataHubEntitiesRequestHandler(IMediator mediator) : IHandler<GetUpdatedDataHubEntitiesRequest, GetDataHubEntitiesResponse>
{
    public async Task<GetDataHubEntitiesResponse> HandleAsync(GetUpdatedDataHubEntitiesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var pageResult = (await mediator.TrySend(new GetDataHubEntitiesQuery()
            {
                WhereClause = $"x.{nameof(DataHubEntity.entityType)} = @entityType and x.lastUpdated >= @fromDateTime",
                PageSize = request.PageSize,
                OrderBy = "x.lastUpdated",
                Select = request.Select,
                ContinuationToken = request.ContinuationToken,
                Parameters = DataHubQueryParameterMapper.Combine(
                    [
                        new QueryParameter("entityType", request.EntityType),
                        new QueryParameter("fromDateTime", request.FromDateTime.ToString(DateFormats.ISO8601))
                    ],
                    DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters))
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            return new GetDataHubEntitiesResponse()
            {
                Success = true,
                Results = pageResult.Results,
                ContinuationToken = pageResult.ContinuationToken,
                ResultCount = pageResult.ResultCount,
                MoreResultsAvailable = pageResult.MoreResultsAvailable
            };
        }
        catch (Exception ex)
        {
            return new GetDataHubEntitiesResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
