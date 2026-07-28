using System.Collections.Generic;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessDeleteDuplicates;

public class ProcessDeleteDuplicatesRequest : IRequest<ProcessDeleteDuplicatesResponse>
{
    public string Where { get; set; }
    public IReadOnlyCollection<QueryParameter> Parameters { get; set; }
}
