using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.GetMergeMarker;

public class GetMergeMarkerRequestValidator : AbstractValidator<GetMergeMarkerRequest>
{
    public GetMergeMarkerRequestValidator()
    {
        RuleFor(r => r.DataSource).NotNull();
        RuleFor(r => r.SourceEntityType).NotNull();
    }
}