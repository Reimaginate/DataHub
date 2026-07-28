using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterMergeSuccesses;

public class RegisterMergeSuccessesRequestValidation : AbstractValidator<RegisterMergeSuccessesRequest>
{
    public RegisterMergeSuccessesRequestValidation()
    {
        RuleFor(r => r.MergeSuccesses).NotEmpty();
    }
}