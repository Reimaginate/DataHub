using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Reimaginate.DataHub.Client;
using Reimaginate.DataHub.CLI.Tools.Shared.Services.Connections;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.CLI.Tools.Shared.API;

public sealed class DataHubCliClient : IDataHubCliClient
{
    private readonly HttpClient _httpClient;
    private readonly IConnectionsService _connectionsService;

    public DataHubCliClient(HttpClient httpClient, IConnectionsService connectionsService)
    {
        _httpClient = httpClient;
        _connectionsService = connectionsService;
        _httpClient.Timeout = TimeSpan.FromMinutes(15);
    }

    public Task<TResponse> PostRequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        where TRequest : DataHubCLIRequest<TResponse>
    {
        request.CorrelationId ??= Guid.NewGuid().ToString("N");
        request.RequestType ??= typeof(TRequest).Name;

        return PostSerializedRequestAsync<TResponse>(new SerializedRequest
        {
            RequestType = request.RequestType,
            CorrelationId = request.CorrelationId,
            TraceOptions = request.TraceOptions,
            Data = JsonConvert.SerializeObject(request, new JsonSerializerSettings { DateParseHandling = DateParseHandling.DateTimeOffset })
        }, cancellationToken);
    }

    public async Task<TResponse> PostSerializedRequestAsync<TResponse>(SerializedRequest request, CancellationToken cancellationToken)
    {
        request.CorrelationId ??= Guid.NewGuid().ToString("N");

        var connection = await _connectionsService.EnsureConnected(cancellationToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, connection.DataHubUrl)
        {
            Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json")
        };

        httpRequest.Headers.TryAddWithoutValidation("x-correlation-id", request.CorrelationId);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new DataHubClientException(
                response.StatusCode,
                response.ReasonPhrase,
                request.RequestType,
                request.CorrelationId,
                responseBody,
                TryDeserializeErrorResponse(responseBody));
        }

        return string.IsNullOrWhiteSpace(responseBody)
            ? default!
            : JsonConvert.DeserializeObject<TResponse>(responseBody)!;
    }

    private static DataHubErrorResponse? TryDeserializeErrorResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<DataHubErrorResponse>(responseBody);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
