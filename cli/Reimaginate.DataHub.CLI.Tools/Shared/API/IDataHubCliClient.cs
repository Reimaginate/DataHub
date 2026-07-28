using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.CLI.Tools.Shared.API;

public interface IDataHubCliClient
{
    Task<TResponse> PostRequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        where TRequest : DataHubCLIRequest<TResponse>;

    Task<TResponse> PostSerializedRequestAsync<TResponse>(SerializedRequest request, CancellationToken cancellationToken);
}
