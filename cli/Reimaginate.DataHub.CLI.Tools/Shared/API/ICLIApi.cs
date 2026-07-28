using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.CLI.Tools.Shared.API;

public interface ICLIApi
{
    Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default);
}
