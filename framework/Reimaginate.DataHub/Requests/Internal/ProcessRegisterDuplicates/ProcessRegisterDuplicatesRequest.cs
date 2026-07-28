using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRegisterDuplicates;

public class ProcessRegisterDuplicatesRequest : IRequest<ProcessRegisterDuplicatesResponse>
{
    public List<Duplicate> Duplicates { get; set; }
    public bool AutoSubmitMergeJobs { get; set; }
}