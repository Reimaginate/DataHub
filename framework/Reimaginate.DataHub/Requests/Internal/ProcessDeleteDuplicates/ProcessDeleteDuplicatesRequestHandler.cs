using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessDeleteDuplicates;

public class ProcessDeleteDuplicatesRequestHandler(IMediator mediator) : IHandler<ProcessDeleteDuplicatesRequest, ProcessDeleteDuplicatesResponse>
{
    public async Task<ProcessDeleteDuplicatesResponse> HandleAsync(ProcessDeleteDuplicatesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var getDuplicatesRequest = new GetCosmosDocumentsQuery<Duplicate>()
            {
                Select = "x.id",
                WhereClause = $"({request.Where})",
                Parameters = request.Parameters
            };

            var getDuplicatesResponse = (await mediator.TrySend(getDuplicatesRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            if (!getDuplicatesResponse.Results.Any())
            {
                return new ProcessDeleteDuplicatesResponse()
                {
                    Success = true
                };
            }

            var deleteFailures = new List<DataAccessFailure<Duplicate>>();
            while (getDuplicatesResponse.Results.Any())
            {
                var deleteRequest = new DeleteCosmosDocumentsCommand<Duplicate>()
                {
                    Documents = getDuplicatesResponse.Results
                };

                var deleteResponse = (await mediator.TrySend(deleteRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                deleteFailures.AddRange(deleteResponse.Failures);

                if (!getDuplicatesResponse.MoreResultsAvailable || deleteResponse.Successes?.Any() != true || deleteResponse.Failures?.Any() == true)
                {
                    break;
                }

                getDuplicatesRequest.ContinuationToken = null;
                getDuplicatesResponse = (await mediator.TrySend(getDuplicatesRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            }

            if (deleteFailures.Any())
            {

                return new ProcessDeleteDuplicatesResponse()
                {
                    Success = false,
                    FailureReason = string.Join("\n", deleteFailures.Select(s => $"{s.Item.id}: {s.Error.Message}"))
                };
            }


            return new ProcessDeleteDuplicatesResponse()
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new ProcessDeleteDuplicatesResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
