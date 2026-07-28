using System;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.Agent;

public class EnableSyncJobsResponse : AgentResponse
{
    public bool Successful { get; set; }
    public List<Exception> Exceptions { get; set; }
}