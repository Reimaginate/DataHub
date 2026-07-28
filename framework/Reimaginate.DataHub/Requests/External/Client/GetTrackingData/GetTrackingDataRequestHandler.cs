using System;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntriesWhere;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.GetTrackingData;

public class GetTrackingDataRequestHandler(IMediator mediator) : IHandler<GetTrackingDataRequest, GetTrackingDataResponse>
{
    public async Task<GetTrackingDataResponse> HandleAsync(GetTrackingDataRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = (await mediator.TrySend(new GetTrackingEntriesWhereQuery()
            {
                From = request.From,
                Select = request.Select,
                WhereClause = request.WhereClause,
                ContinuationToken = request.ContinuationToken,
                OrderBy = request.OrderBy,
                PageSize = request.PageSize,
                GetTotalResultCount = request.GetTotalResultCount,
                Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            return new GetTrackingDataResponse()
            {
                Success = true,
                Results = response.Results,
                ContinuationToken = response.ContinuationToken,
                MoreResultsAvailable = response.MoreResultsAvailable,
                ResultCount = response.ResultCount
            };
        }
        catch (Exception ex)
        {
            return new GetTrackingDataResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
