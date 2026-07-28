using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Commands.Delete.Jobs;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class DeleteJobsByIdCommandTests
{
    [Fact]
    public async Task DeleteJobsById_should_serialize_parameterized_where_clause()
    {
        var api = new CapturingCliApi(new DeleteJobsResponse { Success = true });
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<ICLIApi>(api)
            .BuildServiceProvider();
        var command = new DeleteJobsByIdCommand(serviceProvider);

        var exitCode = await command.HandleCommand(["job'1", "job-2"], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        api.Message.Should().NotBeNull();
        api.Message!.RequestType.Should().Be(nameof(DeleteJobsRequest));
        var request = JsonConvert.DeserializeObject<DeleteJobsRequest>(api.Message.Data);
        request.Should().NotBeNull();
        request!.Where.Should().Be("x.id in (@id0,@id1)");
        request.Parameters.Should().Contain(parameter => parameter.Name == "id0" && Equals(parameter.Value, "job'1"));
        request.Parameters.Should().Contain(parameter => parameter.Name == "id1" && Equals(parameter.Value, "job-2"));
    }

    [Fact]
    public async Task DeleteJobsById_dry_run_should_not_call_api()
    {
        var api = new CapturingCliApi(new DeleteJobsResponse { Success = true });
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<ICLIApi>(api)
            .BuildServiceProvider();
        var command = new DeleteJobsByIdCommand(serviceProvider);

        var exitCode = await command.HandleCommandWithOptions(["job-1"], dryRun: true, yes: false, TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        api.Message.Should().BeNull();
    }

    private sealed class CapturingCliApi(DeleteJobsResponse response) : ICLIApi
    {
        public SerializedRequest? Message { get; private set; }

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Message = message;
            return Task.FromResult((T)(object)response);
        }
    }
}
