using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class ImportEntityRequest : DataHubCLIRequest<ImportEntityResponse>
{
    public ImportEntityRequest()
    {
        RequestType = nameof(ImportEntityRequest);
    }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public bool? Untracked { get; set; }
    public bool DispatchNotifications { get; set; } = false;
    public bool Silent { get; set; }
    public bool? OverwriteIfExists { get; set; }
    public JObject Data { get; set; }

}