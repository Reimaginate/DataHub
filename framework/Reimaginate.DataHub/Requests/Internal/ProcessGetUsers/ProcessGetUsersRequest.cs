using System.Collections.Generic;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessGetUsers;

public class ProcessGetUsersRequest : IRequest<ProcessGetUsersResponse>
{
    public string Where { get; set; }
    public string ContinuationToken { get; set; }
    public IReadOnlyCollection<QueryParameter> Parameters { get; set; }
}
