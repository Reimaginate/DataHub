using System.Collections.Generic;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessDeleteJobs;

public class ProcessDeleteJobsRequest : IRequest<ProcessDeleteJobsResponse>
{
    public List<string> JobIds { get; set; }
    
}