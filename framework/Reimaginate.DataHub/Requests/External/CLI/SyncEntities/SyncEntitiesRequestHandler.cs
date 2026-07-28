using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Requests.Internal.ProcessSubmitJobs;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.SyncEntities;

public class SyncEntitiesRequestHandler(IMediator mediator, IIdService idService) : IHandler<SyncEntitiesRequest, SyncEntitiesResponse>
{
    public async Task<SyncEntitiesResponse> HandleAsync(SyncEntitiesRequest request, CancellationToken cancellationToken)
    {
        var syncEntitiesRequest = new SharedModels.Requests.Agent.SyncEntitiesRequest()
        {
            DataHubEntityType = request.DataHubEntityType,
            DataHubEntityIds = request.DataHubEntityIds
        };

        var job = new JobDTO()
        {
            JobId = idService.NewId<Job>(),
            Type = nameof(SharedModels.Requests.Agent.SyncEntitiesRequest),
            Name = "Sync Entities",
            Target = "DataMaintenanceAgent",
            Status = "Ready",
            Request = JObject.FromObject(syncEntitiesRequest)
        };
        
        var submitJobsRequest = new ProcessSubmitJobsRequest()
        {
            User = request.User,
            DisableNotifications = true,
            Jobs = [job]
        };

        var submitJobsResponse = (await mediator.TrySend(submitJobsRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        if (!submitJobsResponse.Success)
        {
            return new SyncEntitiesResponse()
            {
                Success = false,
                FailureReason = submitJobsResponse.FailureReason
            };
        }

        return new SyncEntitiesResponse()
        {
            Success = true
        };
    }
}
