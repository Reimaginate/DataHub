using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.Requests.Internal.FindDuplicate;

public class FindDuplicateResponse
{
    public JObject DuplicateEntity { get; set; }
}