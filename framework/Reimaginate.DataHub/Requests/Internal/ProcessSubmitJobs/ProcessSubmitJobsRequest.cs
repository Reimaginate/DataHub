using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessSubmitJobs;

public class ProcessSubmitJobsRequest : IRequest<ProcessSubmitJobsResponse>
{
    public List<JobDTO> Jobs { get; set; }
    public User User { get; set; }
    public bool DisableNotifications { get; set; }
}