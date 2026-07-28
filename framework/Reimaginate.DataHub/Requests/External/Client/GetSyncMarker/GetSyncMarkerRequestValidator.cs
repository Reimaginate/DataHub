using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.GetSyncMarker;

public class GetSyncMarkerRequestValidator : AbstractValidator<GetSyncMarkerRequest>
{
    public GetSyncMarkerRequestValidator()
    {
        RuleFor(r => r.DataSource).NotNull();
        RuleFor(r => r.DataHubEntityType).NotNull();
    }
}