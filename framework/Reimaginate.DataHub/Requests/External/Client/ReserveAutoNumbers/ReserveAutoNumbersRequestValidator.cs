using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.ReserveAutoNumbers;

public class ReserveAutoNumbersRequestValidator : AbstractValidator<ReserveAutoNumbersRequest>
{
    public const int MaxCount = 1000;

    public ReserveAutoNumbersRequestValidator()
    {
        RuleFor(r => r.SequenceName).NotEmpty();
        RuleFor(r => r.Count).InclusiveBetween(1, MaxCount);
    }
}
