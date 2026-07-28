using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ResolveEntityReferenceResolutionPromises;

public class ResolveEntityReferenceResolutionPromisesRequest : IRequest<ResolveEntityReferenceResolutionPromisesResponse>
{
    public List<string> SourceSystemEntityIds { get; set; }
    public List<ResolvedEntityReference> ResolvedReferencedEntities { get; set; }
    public bool DoNotTrack { get; set; } = false;
}