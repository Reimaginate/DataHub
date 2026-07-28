using Reimaginate.DataHub.CLI.Tools.Shared.Models;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Auth;

public interface IDataHubCliTokenProvider
{
    Task<string> GetAccessTokenAsync(DataHubConnection connection, CancellationToken cancellationToken);
    Task<string> GetAccessTokenSilentAsync(DataHubConnection connection, CancellationToken cancellationToken);
    Task<string> LoginAsync(DataHubConnection connection, CancellationToken cancellationToken);
    Task LogoutAsync(DataHubConnection connection, CancellationToken cancellationToken);
}
