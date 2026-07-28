using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocument;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.UpdateJobStatus;

public class UpdateJobStatusRequestHandler(IMediator mediator) : IHandler<UpdateJobStatusRequest, UpdateJobStatusResponse>
{
    public async Task<UpdateJobStatusResponse> HandleAsync(UpdateJobStatusRequest request, CancellationToken cancellationToken)
    {
        var getJobResponse = (await mediator.TrySend(new GetCosmosDocumentQuery<Job>()
        {
            Id = request.JobId
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        if (!getJobResponse.Success)
        {
            return new UpdateJobStatusResponse()
            {
                Success = false,
                FailureReason = getJobResponse.FailureReason
            };
        }

        var job = getJobResponse.Result;

        job.Status = request.Status;
        job.Response = request.Response;
        
        var updateJobResponse = (await mediator.TrySend(new UpsertCosmosDocumentsCommand<Job>()
        {
            Documents = [job]
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
        
        if (updateJobResponse.Failures.Any())
        {
            return new UpdateJobStatusResponse()
            {
                Success = false,
                FailureReason = updateJobResponse.Failures[0].Error.Message
            };
        }

        return new UpdateJobStatusResponse()
        {
            Success = true
        };
    }
}