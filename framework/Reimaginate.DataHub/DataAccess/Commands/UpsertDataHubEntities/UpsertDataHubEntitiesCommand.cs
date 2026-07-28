using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.Mediator;

// ReSharper disable IdentifierTypo

namespace Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;

public class UpsertDataHubEntitiesCommand : IRequest<UpsertDataHubEntitiesResponse>
{
    public List<JObject> Entities { get; set; }
}