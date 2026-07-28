using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.CLI.Tools.Shared.API;

public sealed class DataHubCliApi(IDataHubCliClient client) : ICLIApi
{
    public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        => client.PostSerializedRequestAsync<T>(message, cancellationToken);
}
