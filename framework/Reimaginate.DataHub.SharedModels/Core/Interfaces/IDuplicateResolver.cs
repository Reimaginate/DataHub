using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.SharedModels.Core.Interfaces;

public interface IDuplicateResolver
{
    Task<JArray> FindPotentialDuplicatesAsync(IMediator mediator, string dataHubEntityType, List<JObject> i, CancellationToken cancellationToken);
    IEnumerable<JToken> Resolve(JObject i, JArray potentialDuplicates);
}