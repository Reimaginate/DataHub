using System.Collections.Generic;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DeleteTrackingDataEntries;

public class DeleteTrackingDataEntriesRequest : IRequest<DeleteTrackingDataResponse>
{
    public List<string> Ids { get; set; }
}