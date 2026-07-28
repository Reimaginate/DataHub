using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateDuplicates;

public class ProcessUpdateDuplicatesRequest : IRequest<ProcessUpdateDuplicatesResponse>
{
    public List<Duplicate> Duplicates { get; set; }
}