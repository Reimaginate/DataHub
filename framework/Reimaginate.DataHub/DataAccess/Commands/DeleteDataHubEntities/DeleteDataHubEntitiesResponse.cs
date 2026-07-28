using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Models;

namespace Reimaginate.DataHub.DataAccess.Commands.DeleteDataHubEntities;

public class DeleteDataHubEntitiesResponse
{
    public List<JObject> Successes { get; set; }

    public List<DataAccessFailure<JObject>> Failures { get; set; }
}