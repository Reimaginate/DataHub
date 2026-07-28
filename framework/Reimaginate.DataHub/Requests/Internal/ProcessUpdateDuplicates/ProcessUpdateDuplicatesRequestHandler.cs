using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateDuplicates;

public class ProcessUpdateDuplicatesRequestHandler(IMediator mediator) : IHandler<ProcessUpdateDuplicatesRequest, ProcessUpdateDuplicatesResponse>
{
    public async Task<ProcessUpdateDuplicatesResponse> HandleAsync(ProcessUpdateDuplicatesRequest request, CancellationToken cancellationToken)
    {
        var req = new UpsertCosmosDocumentsCommand<Duplicate>()
        {
            Documents = request.Duplicates
        };

        try
        {
            var response = (await mediator.TrySend(req, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            if (response.Failures.Any())
            {
                return new ProcessUpdateDuplicatesResponse()
                {
                    Success = false,
                    FailureReason = string.Join(",", response.Failures.Select(s => $"{s.Item.id}: {s.Error.Message}"))
                };
            }

            return new ProcessUpdateDuplicatesResponse()
            {
                Success = true,
            };
        }
        catch (Exception ex)
        {
            return new ProcessUpdateDuplicatesResponse()
            {
                Success = false,
                FailureReason = ex.Message
            };

        }
    }
}