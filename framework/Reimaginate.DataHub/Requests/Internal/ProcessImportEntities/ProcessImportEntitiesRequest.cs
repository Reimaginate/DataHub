using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessImportEntities;

public class ProcessImportEntitiesRequest : IRequest<List<ImportEntityResponse>>
{
    public string CorrelationId { get; set; }
    public List<ImportEntityRequest> ImportEntityRequests { get; set; }

    public bool DispatchNotifications { get; set; }

    public bool Silent { get; set; }

    public bool? Untracked { get; set; }

    public bool? OverwriteIfExists { get; set; }
}