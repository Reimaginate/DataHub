using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.Requests.Internal.ProcessSubmitJobs;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs.JobRequests;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRegisterDuplicates;

public class ProcessRegisterDuplicatesRequestHandler(IIdService idService, IMediator mediator, ITimeService timeService) : IHandler<ProcessRegisterDuplicatesRequest, ProcessRegisterDuplicatesResponse>
{
    public async Task<ProcessRegisterDuplicatesResponse> HandleAsync(ProcessRegisterDuplicatesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var duplicatesToCreate = new List<Duplicate>(request.Duplicates);
            var failures = new List<string>();

            while (duplicatesToCreate.Any())
            {
                var batch = duplicatesToCreate.Take(500).ToList();

                var createDuplicatesResponse = (await mediator.TrySend(new CreateCosmosDocumentsCommand<Duplicate>()
                {
                    Documents = request.Duplicates
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                if (createDuplicatesResponse.Failures.Any())
                {
                    failures.AddRange(createDuplicatesResponse.Failures.Select(s => $"{s.Item.id}: {s.Error.Message}"));
                    //TODO:
                }

                if (request.AutoSubmitMergeJobs)
                {
                    var duplicateMergeRequests = createDuplicatesResponse.Successes.Select(d => new DuplicateMergeRequest()
                    {
                        DuplicateId = d.id,
                        MergePlan = new DuplicateMergePlan()
                        {
                            EntityType = d.EntityType,
                            EntityIds = d.EntityIds,
                            AutoMerge = true
                        }
                    }).ToList();

                    //Submit Jobs
                    var duplicateMergeJobs = duplicateMergeRequests.Select(dupReq =>
                    {
                        var now = timeService.Now();
                        return new JobDTO()
                        {
                            Type = nameof(DuplicateMergeRequest),
                            JobId = idService.NewId<DuplicateMergeRequest>(),
                            CreatedOn = now,
                            LastUpdated = now,
                            Name = $"{nameof(DuplicateMergeRequest)}: {dupReq.DuplicateId}",
                            Status = "Ready",
                            Target = "DataMaintenanceAgent",
                            Request = JToken.FromObject(dupReq)
                        };
                    }).ToList();

                    var submitJobsResponse = (await mediator.TrySend(new ProcessSubmitJobsRequest()
                    {
                        Jobs = duplicateMergeJobs,
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    if (!submitJobsResponse.Success)
                    {
                        //TODO
                    }
                }

                duplicatesToCreate.RemoveRange(0, batch.Count);
            }

            if (failures.Any())
            {
                return new ProcessRegisterDuplicatesResponse()
                {
                    Success = false,
                    FailureReason = string.Join("\n", failures)
                };
            }

            return new ProcessRegisterDuplicatesResponse()
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new ProcessRegisterDuplicatesResponse()
            {
                Success = false,
                FailureReason = ex.Message,
            };
        }
    }
}