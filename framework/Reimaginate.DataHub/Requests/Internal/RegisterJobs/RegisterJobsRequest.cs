using System.Collections.Generic;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.RegisterJobs;

public class RegisterJobsRequest : IRequest<RegisterJobsResponse>
{
    public RegisterJobsRequest() { }

    public RegisterJobsRequest(List<RegisterJobRequest> jobs) => JobRequests = jobs;
    public List<RegisterJobRequest> JobRequests { get; set; }
}