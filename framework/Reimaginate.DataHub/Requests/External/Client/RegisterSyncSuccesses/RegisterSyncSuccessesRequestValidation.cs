using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterSyncSuccesses;

public class RegisterSyncSuccessesRequestValidation : AbstractValidator<RegisterSyncSuccessesRequest>
{
    public RegisterSyncSuccessesRequestValidation()
    {
        RuleFor(r => r.SyncSuccesses).NotEmpty();
    }
}