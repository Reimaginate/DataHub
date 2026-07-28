using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Reimaginate.DataHub.Auth;

public interface IDataHubSeedAdminService
{
    Task SeedAdminsAsync(IEnumerable<DataHubSeedAdminUser> users, CancellationToken cancellationToken = default);
}
