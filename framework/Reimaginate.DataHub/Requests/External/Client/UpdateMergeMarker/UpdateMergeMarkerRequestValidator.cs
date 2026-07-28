using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.UpdateMergeMarker;

public class UpdateMergeMarkerRequestValidator : AbstractValidator<UpdateMergeMarkerRequest>
{
    public UpdateMergeMarkerRequestValidator()
    {
        RuleFor(r => r.MergeMarker).NotNull();
        RuleFor(r => r.MergeMarker.id).NotEmpty();
        RuleFor(r => r.MergeMarker.AgentId).NotEmpty();
        RuleFor(r => r.MergeMarker.DataSource).NotEmpty();
        RuleFor(r => r.MergeMarker.EntityType).NotEmpty();
        RuleFor(r => r.NewValue).NotEmpty();
    }
}