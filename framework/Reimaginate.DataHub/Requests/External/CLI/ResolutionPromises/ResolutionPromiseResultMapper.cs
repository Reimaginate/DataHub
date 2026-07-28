using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.ResolutionPromises;

internal static class ResolutionPromiseResultMapper
{
    public static ResolutionPromiseResult ToResult(ResolutionPromise promise)
    {
        var externalReference = promise.ExternalEntityReference;
        return new ResolutionPromiseResult
        {
            PromiseId = promise.id,
            DataHubEntityType = promise.DataHubEntityType,
            DataHubEntityId = promise.DataHubEntityId,
            EntityReferencePath = promise.EntityReferencePath,
            DataSource = externalReference?.DataSource,
            SourceEntityType = externalReference?.SourceEntityType,
            SourceEntityId = externalReference?.EntityId,
            TargetEntityType = externalReference?.EntityType
        };
    }

    public static DeleteResolutionPromiseResult ToDeleteResult(ResolutionPromise promise, string status, string reason = null)
    {
        var result = ToResult(promise);
        return new DeleteResolutionPromiseResult
        {
            PromiseId = result.PromiseId,
            DataHubEntityType = result.DataHubEntityType,
            DataHubEntityId = result.DataHubEntityId,
            EntityReferencePath = result.EntityReferencePath,
            DataSource = result.DataSource,
            SourceEntityType = result.SourceEntityType,
            SourceEntityId = result.SourceEntityId,
            TargetEntityType = result.TargetEntityType,
            Status = status,
            Reason = reason
        };
    }
}
