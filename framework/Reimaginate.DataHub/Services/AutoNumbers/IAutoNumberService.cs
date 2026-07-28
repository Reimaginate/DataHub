using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Services.AutoNumbers;

public interface IAutoNumberService
{
    Task<List<ReservedAutoNumber>> ReserveAsync(string sequenceName, int count, CancellationToken cancellationToken);
}
