using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DetachEntitiesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<JObject> ResultingEntities { get; set; } = new();
    public List<Exception> Failures { get; set; } = new();
}