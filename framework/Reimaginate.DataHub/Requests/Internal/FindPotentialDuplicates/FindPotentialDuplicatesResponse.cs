using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.Requests.Internal.FindPotentialDuplicates;

public class FindPotentialDuplicatesResponse
{
    public JArray PotentialDuplicates { get; set; }
}