using System.Collections.Generic;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessGetJobs;

public class ProcessGetJobsRequest : IRequest<ProcessGetJobsResponse>
{
    public string Select { get; set; }
    public string Where { get; set; }
    public string OrderBy { get; set; }
    public int? PageSize { get; set; }
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; }
    public IReadOnlyCollection<QueryParameter> Parameters { get; set; }
}
