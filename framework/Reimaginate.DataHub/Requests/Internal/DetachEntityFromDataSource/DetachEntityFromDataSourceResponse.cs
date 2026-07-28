using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.Requests.Internal.DetachEntityFromDataSource;

public sealed class DetachEntityFromDataSourceResponse
{
    public JObject ResultingEntity { get; set; }
}