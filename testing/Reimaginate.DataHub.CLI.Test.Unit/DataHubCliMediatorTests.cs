using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.Config;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Requests.Send;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class DataHubRequestHandlerTests
{
    [Fact]
    public async Task SendRequest_serializes_get_users_request()
    {
        var api = new CapturingCliApi();
        var mediator = CreateMediator(api);
        var request = new GetUsersRequest
        {
            CorrelationId = "get-users-correlation",
            Where = "Name == 'Craig'"
        };

        var response = (await mediator.TrySend<SendResponse>(new SendRequest(request), CancellationToken.None)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResponse } => mediatorResponse! };

        response.Result.Value<string>().Should().Be("ok");
        api.Message!.RequestType.Should().Be(nameof(GetUsersRequest));
        api.Message.CorrelationId.Should().Be("get-users-correlation");
        JObject.Parse(api.Message.Data)[nameof(GetUsersRequest.Where)]!.Value<string>().Should().Be("Name == 'Craig'");
    }

    [Fact]
    public async Task SendRequest_serializes_delete_jobs_where_request()
    {
        var api = new CapturingCliApi();
        var mediator = CreateMediator(api);
        var request = new DeleteJobsRequest
        {
            CorrelationId = "delete-jobs-correlation",
            Where = "Status == 'Completed'"
        };

        _ = (await mediator.TrySend<SendResponse>(new SendRequest(request), CancellationToken.None)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResponse } => mediatorResponse! };

        api.Message!.RequestType.Should().Be(nameof(DeleteJobsRequest));
        api.Message.CorrelationId.Should().Be("delete-jobs-correlation");
        JObject.Parse(api.Message.Data)[nameof(DeleteJobsRequest.Where)]!.Value<string>().Should().Be("Status == 'Completed'");
    }

    private static IMediator CreateMediator(ICLIApi api)
    {
        var services = new ServiceCollection()
            .AddSingleton(api);
        services.AddDataHubCliCommands(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    private sealed class CapturingCliApi : ICLIApi
    {
        public SerializedRequest? Message { get; private set; }

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Message = message;
            object response = JValue.CreateString("ok");
            return Task.FromResult((T)response);
        }
    }
}
