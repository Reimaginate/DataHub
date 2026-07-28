using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.Mediator;

// ReSharper disable IdentifierTypo

namespace Reimaginate.DataHub.DataAccess.Commands.CreateDataHubEntities;

public class CreateDataHubEntitiesCommand : IRequest<CreateDataHubEntitiesResponse>
{
    public List<JObject> Entities { get; set; }
}