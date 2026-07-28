using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class ResolveResolutionPromisesRequest : DataHubCLIRequest<ResolveResolutionPromisesResponse>
{
    public ResolveResolutionPromisesRequest()
    {
        RequestType = nameof(ResolveResolutionPromisesRequest);
    }

    public List<string> PromiseIds { get; set; } = [];
    public string WhereClause { get; set; }
    public int PageSize { get; set; } = 1000;
    public string ContinuationToken { get; set; }
    public bool DryRun { get; set; }
    public bool DoNotTrack { get; set; }
    public bool StopOnFailure { get; set; }
}

public class ResolveResolutionPromisesResponse
{
    public int MatchedCount { get; set; }
    public int ResolvedCount { get; set; }
    public int UnresolvedCount { get; set; }
    public int DeletedStaleCount { get; set; }
    public int FailedCount { get; set; }
    public string ContinuationToken { get; set; }
    public bool MoreResultsAvailable { get; set; }
    public List<ResolveResolutionPromiseResult> Results { get; set; } = [];
}

public class ResolveResolutionPromiseResult
{
    public string PromiseId { get; set; }
    public string DataHubEntityType { get; set; }
    public string DataHubEntityId { get; set; }
    public string EntityReferencePath { get; set; }
    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public string SourceEntityId { get; set; }
    public string TargetEntityType { get; set; }
    public string ResolvedEntityType { get; set; }
    public string ResolvedEntityId { get; set; }
    public string Status { get; set; }
    public string Reason { get; set; }
}
