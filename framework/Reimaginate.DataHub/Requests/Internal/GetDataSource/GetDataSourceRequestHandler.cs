using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.GetDataSource;

public class GetDataSourceRequestHandler(IMediator mediator) : IHandler<GetDataSourceRequest, DataSource>
{
    public async Task<DataSource> HandleAsync(GetDataSourceRequest request, CancellationToken cancellationToken)
    {
        var queryResponse = (await mediator.TrySend(new GetCosmosDocumentsQuery<DataSource>()
        {
            WhereClause = "x.name = @dataSourceName",
            Parameters = [new QueryParameter("dataSourceName", request.DataSourceName)]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return queryResponse.Results.FirstOrDefault();
    }
}
