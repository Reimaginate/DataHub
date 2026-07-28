using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Models;

namespace Reimaginate.DataHub.DataAccess.Commands.CreateDataHubEntities;

public class CreateDataHubEntitiesResponse
{
    public List<JObject> Successes { get; set; }

    public List<DataAccessFailure<JObject>> Failures { get; set; }
}