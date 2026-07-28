using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.GetDuplicate;

public class GetDuplicateRequestValidator : AbstractValidator<GetDuplicateRequest>
{
    public GetDuplicateRequestValidator()
    {
        RuleFor(r => r.Id).NotEmpty();
    }
}