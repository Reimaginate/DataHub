using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class ResolveEntityReferencesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<ResolvedEntityReference> Results { get; set; } = new();
    public List<ResolveEntityReferenceException> ResolutionFailures { get; set; } = new();
}