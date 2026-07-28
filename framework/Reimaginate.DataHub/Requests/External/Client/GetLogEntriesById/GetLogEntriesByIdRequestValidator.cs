using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.GetLogEntriesById;

public class GetLogEntriesByIdRequestValidator : AbstractValidator<GetLogEntriesByIdRequest>
{
    public GetLogEntriesByIdRequestValidator()
    {
        RuleFor(f => f.Ids).NotEmpty();
    }
}