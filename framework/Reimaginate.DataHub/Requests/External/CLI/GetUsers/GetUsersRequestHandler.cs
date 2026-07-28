using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.Requests.Internal.ProcessGetUsers;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mapper;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.GetUsers;

public class GetUsersRequestHandler(IMediator mediator, IMapper mapper) : IHandler<GetUsersRequest, GetUsersResponse>
{
    public async Task<GetUsersResponse> HandleAsync(GetUsersRequest request, CancellationToken cancellationToken)
    {
        var getUsersResponse = (await mediator.TrySend(new ProcessGetUsersRequest()
        {
            Where = request.Where,
            ContinuationToken = request.ContinuationToken,
            Parameters = DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters)
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (!getUsersResponse.Success)
        {
            return new GetUsersResponse()
            {
                Success = false,
                FailureReason = getUsersResponse.FailureReason
            };
        }

        var mappedResults = await mapper.MapAsync<List<UserDTO>>(getUsersResponse.PagedResults.Results, cancellationToken);
        var ret = new PagedResults<UserDTO>()
        {
            Results = mappedResults,
            ContinuationToken = getUsersResponse.PagedResults.ContinuationToken,
            ResultCount = getUsersResponse.PagedResults.ResultCount
        };


        return new GetUsersResponse()
        {
            Success = true,
            Results = ret.Results.Select(JObject.FromObject).ToList(),
            ContinuationToken = ret.ContinuationToken,
            MoreResultsAvailable = ret.MoreResultsAvailable,
            ResultCount = ret.ResultCount
        };
    }
}
