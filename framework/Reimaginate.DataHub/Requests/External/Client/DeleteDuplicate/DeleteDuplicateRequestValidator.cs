using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;


namespace Reimaginate.DataHub.Requests.External.Client.DeleteDuplicate;

public class DeleteDuplicateRequestValidator : AbstractValidator<DeleteDuplicateRequest>
{
    public DeleteDuplicateRequestValidator()
    {
        RuleFor(r => r.Id).NotEmpty();
    }
}