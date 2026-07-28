using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Commands.DeleteDataHubEntities;

public class DeleteDataHubEntitiesCommand : IRequest<DeleteDataHubEntitiesResponse>
{
    public List<JObject> Entities { get; set; }
}