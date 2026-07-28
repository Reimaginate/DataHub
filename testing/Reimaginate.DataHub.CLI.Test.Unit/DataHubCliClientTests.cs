using System.Net;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Models;
using Reimaginate.DataHub.CLI.Tools.Shared.Services.Connections;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class DataHubCliClientTests
{
    [Fact]
    public async Task PostRequestAsync_sends_bearer_auth_correlation_header_and_serialized_envelope()
    {
        var handler = new CapturingHandler(JsonConvert.SerializeObject(new GetUsersResponse { Success = true }));
        var connection = new DataHubConnection
        {
            Name = "dev",
            DataHubUrl = "https://datahub.test/api/cli",
            AccessToken = "access-token"
        };
        var client = new DataHubCliClient(new HttpClient(handler), new FakeConnectionsService(connection));
        var request = new GetUsersRequest
        {
            CorrelationId = "correlation-1",
            Where = "Name == 'Craig'",
            TraceOptions = new DataHubTraceOptions
            {
                Enabled = true,
                IncludeRequest = true,
                IncludeResponse = true,
                Reason = "cli diagnostics"
            }
        };

        var response = await client.PostRequestAsync<GetUsersRequest, GetUsersResponse>(request, CancellationToken.None);

        response.Success.Should().BeTrue();
        handler.Request!.RequestUri.Should().Be(connection.DataHubUrl);
        handler.Request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Request.Headers.Authorization!.Parameter.Should().Be("access-token");
        handler.Request.Headers.GetValues("x-correlation-id").Should().ContainSingle("correlation-1");

        var envelope = JsonConvert.DeserializeObject<SerializedRequest>(handler.Body!)!;
        envelope.RequestType.Should().Be(nameof(GetUsersRequest));
        envelope.CorrelationId.Should().Be("correlation-1");
        envelope.TraceOptions.Should().NotBeNull();
        envelope.TraceOptions.Enabled.Should().BeTrue();
        envelope.TraceOptions.IncludeRequest.Should().BeTrue();
        envelope.TraceOptions.IncludeResponse.Should().BeTrue();
        envelope.TraceOptions.Reason.Should().Be("cli diagnostics");
        var payload = JObject.Parse(envelope.Data);
        payload[nameof(GetUsersRequest.Where)]!.Value<string>().Should().Be("Name == 'Craig'");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public CapturingHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody)
            };
        }
    }

    private sealed class FakeConnectionsService : IConnectionsService
    {
        public FakeConnectionsService(DataHubConnection currentDataHubConnection)
        {
            CurrentDataHubConnection = currentDataHubConnection;
        }

        public DataHubConnection CurrentDataHubConnection { get; set; }

        public Task<DataHubConnection> ResolveConnectionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(CurrentDataHubConnection);
        }

        public Task<DataHubConnection> EnsureConnected(CancellationToken cancellationToken)
        {
            return Task.FromResult(CurrentDataHubConnection);
        }
    }
}
