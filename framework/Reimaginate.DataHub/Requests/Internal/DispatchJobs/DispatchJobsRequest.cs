using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DispatchJobs;

public class DispatchJobsRequest : IRequest<DispatchJobsResponse>
{
    public List<JobDTO> Jobs { get; set; }
}