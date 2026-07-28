using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Failures;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DeleteLogEntries;

public class DeleteLogEntriesRequestHandler(IMediator mediator) : IHandler<DeleteLogEntriesRequest, DeleteLogEntriesResponse>
{
    public async Task<DeleteLogEntriesResponse> HandleAsync(DeleteLogEntriesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var failures = new List<DataAccessFailure<LogEntry>>();

            var idsToDelete = request.Ids.Distinct().ToList();
            while (idsToDelete.Any())
            {
                var batch = idsToDelete.Take(500).ToList();
                var parameters = new List<QueryParameter>();
                var idParameterNames = DataHubQueryParameterMapper.AddIndexedParameters(batch, "id", parameters);

                var getLogEntriesRequest = new GetCosmosDocumentsQuery<LogEntry>()
                {
                    Select = "x.id, x.Type",
                    WhereClause = $"x.id in ({string.Join(",", idParameterNames)})",
                    Parameters = parameters
                };

                var getLogEntriesResponse = (await mediator.TrySend(getLogEntriesRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                
                while (getLogEntriesResponse.Results.Any())
                {
                    var deleteResponse = (await mediator.TrySend(new DeleteCosmosDocumentsCommand<LogEntry>()
                    {
                        Documents = getLogEntriesResponse.Results.Select(log => new LogEntry() { Type = log.Type, id = log.id }).ToList()
                    }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                    failures.AddRange(deleteResponse.Failures);

                    if (!getLogEntriesResponse.MoreResultsAvailable || deleteResponse.Successes?.Any() != true || deleteResponse.Failures?.Any() == true)
                    {
                        break;
                    }

                    getLogEntriesRequest.ContinuationToken = null;
                    getLogEntriesResponse = (await mediator.TrySend(getLogEntriesRequest, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                }

                idsToDelete.RemoveRange(0, batch.Count);
            }

            if (failures.Any())
            {
                return new DeleteLogEntriesResponse()
                {
                    Success = false,
                    FailureReason = "ONE_OR_MORE_ITEMS_FAILED_TO_DELETE",
                    DeleteFailures = failures.Select(s => new DeleteLogFailure()
                    {
                        LogEntryId = s.Item.id,
                        FailureReason = s.Error?.Message
                    }).ToList()
                };
            }

            return new DeleteLogEntriesResponse()
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new DeleteLogEntriesResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };
        }
    }
}
