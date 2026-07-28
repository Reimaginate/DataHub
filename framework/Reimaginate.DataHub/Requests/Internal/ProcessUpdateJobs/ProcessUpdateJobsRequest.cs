using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateJobs;

public class ProcessUpdateJobsRequest : IRequest<ProcessUpdateJobsResponse>
{
    public List<JobDTO> Jobs { get; set; }
}