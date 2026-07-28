using System;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Newtonsoft.Json;
using Reimaginate.DataHub.Client.Config;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Client
{
    public sealed class DataHubClient : IDataHubClient
    {
        private readonly HttpClient _httpClient;
        private readonly DataHubClientOptions _options;
        private readonly TokenCredential _credential;
        private readonly string _dataHubUrl;

        public DataHubClient(HttpClient httpClient, DataHubClientOptions options)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(options);

            var credential = options.AuthenticationMode == DataHubClientAuthenticationMode.SharedKey
                ? null
                : DataHubClientCredentialFactory.CreateCredential(options);

            DataHubClientCredentialFactory.Validate(options);

            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _options = options;
            _credential = credential;
            _dataHubUrl = options.DataHubClientUrl;
        }

        public DataHubClient(HttpClient httpClient, DataHubClientOptions options, TokenCredential credential)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(options);

            DataHubClientCredentialFactory.Validate(options);
            if (options.AuthenticationMode != DataHubClientAuthenticationMode.SharedKey)
            {
                ArgumentNullException.ThrowIfNull(credential);
            }

            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _options = options;
            _credential = credential;
            _dataHubUrl = options.DataHubClientUrl;
        }

        public async Task<TResponse> PostRequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken) where TRequest : DataHubClientRequest<TResponse> where TResponse : class
        {
            request.CorrelationId ??= Guid.NewGuid().ToString("N");

            var postBody = new SerializedRequest()
            {
                RequestType = request.RequestType,
                CorrelationId = request.CorrelationId,
                TraceOptions = request.TraceOptions as DataHubTraceOptions,
                Data = JsonConvert.SerializeObject(request, new JsonSerializerSettings() { DateParseHandling = DateParseHandling.DateTimeOffset })
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _dataHubUrl)
            {
                Content = new StringContent(JsonConvert.SerializeObject(postBody), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.TryAddWithoutValidation("x-correlation-id", request.CorrelationId);
            await AuthorizeAsync(httpRequest, cancellationToken);

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

            if (string.IsNullOrEmpty(responseBody)) return null;

            var ret = JsonConvert.DeserializeObject<TResponse>(responseBody);
            return ret;
        }

        private async Task AuthorizeAsync(HttpRequestMessage httpRequest, CancellationToken cancellationToken)
        {
            switch (_options.AuthenticationMode)
            {
                case DataHubClientAuthenticationMode.SharedKey:
                    httpRequest.Headers.TryAddWithoutValidation("x-functions-key", _options.Key);
                    break;
                case DataHubClientAuthenticationMode.ApplicationRegistration:
                case DataHubClientAuthenticationMode.ManagedIdentity:
                    var token = await _credential.GetTokenAsync(
                        new TokenRequestContext(new[] { _options.AzureAdScope }),
                        cancellationToken);
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported DataHub client authentication mode '{_options.AuthenticationMode}'.");
            }
        }

        private static DataHubErrorResponse TryDeserializeErrorResponse(string responseBody)
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
}
