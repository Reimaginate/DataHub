namespace Reimaginate.DataHub;

public interface IDataHubClient
{
    Task<TResponse> PostRequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken) where TRequest : DataHubClientRequest<TResponse> where TResponse : class;
}