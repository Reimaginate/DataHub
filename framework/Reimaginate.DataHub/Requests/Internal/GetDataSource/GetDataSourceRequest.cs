using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.GetDataSource;

public class GetDataSourceRequest : IRequest<DataSource>
{
    public string DataSourceName { get; set; }
}