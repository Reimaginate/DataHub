using System;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.Services.AutoNumbers;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.ReserveAutoNumbers;

public class ReserveAutoNumbersRequestHandler(IAutoNumberService autoNumberService) : IHandler<ReserveAutoNumbersRequest, ReserveAutoNumbersResponse>
{
    public async Task<ReserveAutoNumbersResponse> HandleAsync(ReserveAutoNumbersRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var numbers = await autoNumberService.ReserveAsync(request.SequenceName, request.Count, cancellationToken);
            return new ReserveAutoNumbersResponse
            {
                Success = true,
                SequenceName = request.SequenceName,
                Numbers = numbers
            };
        }
        catch (AutoNumberSequenceNotFoundException ex)
        {
            return CreateFailureResponse(request.SequenceName, ex.Message);
        }
        catch (AutoNumberReservationException ex)
        {
            return CreateFailureResponse(request.SequenceName, ex.Message);
        }
        catch (Exception ex)
        {
            return CreateFailureResponse(request.SequenceName, ex.Message);
        }
    }

    private static ReserveAutoNumbersResponse CreateFailureResponse(string sequenceName, string failureReason)
    {
        return new ReserveAutoNumbersResponse
        {
            Success = false,
            SequenceName = sequenceName,
            FailureReason = failureReason
        };
    }
}
