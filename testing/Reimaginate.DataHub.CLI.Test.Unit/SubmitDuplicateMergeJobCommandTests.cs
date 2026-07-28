using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.Commands.Submit.DuplicateMergeJob;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs.JobRequests;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class SubmitDuplicateMergeJobCommandTests
{
    [Fact]
    public async Task HandleCommand_submits_duplicate_merge_as_current_submit_job_request()
    {
        var api = new CapturingCliApi();
        var services = new ServiceCollection();
        services.AddSingleton<ICLIApi>(api);
        using var provider = services.BuildServiceProvider();
        var command = new SubmitDuplicateMergeJobCommand(provider);

        var exitCode = await command.HandleCommand(
            "contact",
            ["contact-1", "contact-2"],
            "contact-1",
            automerge: false,
            dataFilePath: string.Empty,
            CancellationToken.None);

        exitCode.Should().Be(1);
        api.Message.Should().NotBeNull();
        api.Message!.RequestType.Should().Be(nameof(SubmitJobRequest));
        var submitJobRequest = JsonConvert.DeserializeObject<SubmitJobRequest>(api.Message.Data)!;
        submitJobRequest.RequestType.Should().Be(nameof(SubmitJobRequest));
        submitJobRequest.Type.Should().Be(nameof(DuplicateMergeRequest));
        submitJobRequest.Target.Should().Be("DataMaintenanceAgent");

        var duplicateMergeRequest = submitJobRequest.Request.ToObject<DuplicateMergeRequest>()!;
        duplicateMergeRequest.MergePlan.EntityType.Should().Be("contact");
        duplicateMergeRequest.MergePlan.EntityIds.Should().Equal("contact-1", "contact-2");
        duplicateMergeRequest.MergePlan.SurvivingEntityId.Should().Be("contact-1");
        duplicateMergeRequest.MergePlan.AutoMerge.Should().BeFalse();
    }

    private sealed class CapturingCliApi : ICLIApi
    {
        public SerializedRequest? Message { get; private set; }

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Message = message;
            object response = new SubmitJobResponse
            {
                Success = true,
                Result = new JobDTO { JobId = "job-1", Request = new JObject(), Response = new JObject() }
            };

            return Task.FromResult((T)response);
        }
    }
}
