using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntriesForEntity;

public class GetTrackingEntriesForEntityQuery : IRequest<List<ChangeTrackingEntry>>
{
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
}