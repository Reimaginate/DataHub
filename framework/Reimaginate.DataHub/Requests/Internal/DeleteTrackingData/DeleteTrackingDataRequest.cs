using System.Collections.Generic;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DeleteTrackingData;

public class DeleteTrackingDataRequest : IRequest<DeleteTrackingDataResponse>
{
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
}