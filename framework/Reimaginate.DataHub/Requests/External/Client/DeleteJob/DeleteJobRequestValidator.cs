using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.DeleteJob;

public class DeleteJobRequestValidator : AbstractValidator<DeleteJobRequest>
{
    public DeleteJobRequestValidator()
    {
        RuleFor(r => r.JobId).NotEmpty();
    }
}