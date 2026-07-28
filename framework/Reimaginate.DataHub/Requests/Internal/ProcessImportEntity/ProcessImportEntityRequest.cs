using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessImportEntity;

public class ProcessImportEntityRequest : IRequest<ImportEntityResponse>
{
    public string CorrelationId { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public bool? Untracked { get; set; }
    public bool? OverwriteIfExists { get; set; }
    public JObject Data { get; set; }
}