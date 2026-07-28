using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Requests.Internal.ProcessSubmitJobs;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.MergeEntities;

public class MergeEntitiesHandler(IIdService idService, IMediator mediator, ITimeService timeService) : IHandler<MergeEntitiesRequest, MergeEntitiesResponse>
{
    public async Task<MergeEntitiesResponse> HandleAsync(MergeEntitiesRequest request, CancellationToken cancellationToken)
    {

        var mergeEntitiesRequest = new SharedModels.Requests.Agent.MergeEntitiesRequest()
        {
            DataHubEntityType = request.DataHubEntityType,
            EntityIds = request.SourceEntityIds
        };

        var now = timeService.Now();

        var jobs = new List<JobDTO>
        {
            new()
            {
                JobId = idService.NewId<Job>(),
                Type = nameof(SharedModels.Requests.Agent.MergeEntitiesRequest),
                Name = "Merge Entities",
                Target = "DataMaintenanceAgent",
                Status = "Ready",
                CreatedOn = now,
                LastUpdated = now,
                Request = JToken.FromObject(mergeEntitiesRequest)
            }
        };

        var submitJobsRequest = new ProcessSubmitJobsRequest()
        {
            User = request.User,
            DisableNotifications = true,
            Jobs = jobs
        };

        var submitJobsResponse = (await mediator.TrySend(submitJobsRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        if (!submitJobsResponse.Success)
        {
            return new MergeEntitiesResponse()
            {
                Success = false,
                FailureReason = submitJobsResponse.FailureReason
            };
        }

        return new MergeEntitiesResponse()
        {
            Success = true
        };
    }
}
