using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.Diagnostics;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.Mapper;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateJobs;

public class ProcessUpdateJobsRequestHandler(IMediator mediator, IMapper mapper) : IHandler<ProcessUpdateJobsRequest, ProcessUpdateJobsResponse>
{
    public async Task<ProcessUpdateJobsResponse> HandleAsync(ProcessUpdateJobsRequest request, CancellationToken cancellationToken)
    {
        var jobs = await mapper.MapAsync<List<Job>>(request.Jobs, cancellationToken);
        
        var req = new UpsertCosmosDocumentsCommand<Job>()
        {
            Documents = jobs
        };

        try
        {
            var response = (await mediator.TrySend(req, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            if (response.Failures.Any())
            {
                DataHubTelemetry.RecordPersistenceFailure("job_upsert", nameof(Job), response.Failures.LongCount());
                return new ProcessUpdateJobsResponse()
                {
                    Success = false,
                    FailureReason = string.Join(",", response.Failures.Select(s => $"{s.Item.id}: {s.Error.Message}"))
                };
            }

            foreach (var job in jobs)
            {
                DataHubTelemetry.RecordJobStatus(job.Status, job.Type, job.Target);
            }

            return new ProcessUpdateJobsResponse()
            {
                Success = true,
            };
        }
        catch (Exception ex)
        {
            return new ProcessUpdateJobsResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };

        }
    }
}
