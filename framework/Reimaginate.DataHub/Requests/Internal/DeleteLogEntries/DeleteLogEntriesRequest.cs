using System.Collections.Generic;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DeleteLogEntries;

public class DeleteLogEntriesRequest : IRequest<DeleteLogEntriesResponse>
{
    public List<string> Ids { get; set; }
}