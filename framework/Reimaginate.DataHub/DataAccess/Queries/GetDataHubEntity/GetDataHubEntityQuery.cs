using Newtonsoft.Json.Linq;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetDataHubEntity;

public class GetDataHubEntityQuery : IRequest<JObject>
{
    public string EntityType { get; set; }
    public string Id { get; set; }
}