using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class ListResolutionPromisesRequest : DataHubCLIRequest<ListResolutionPromisesResponse>
{
    public ListResolutionPromisesRequest()
    {
        RequestType = nameof(ListResolutionPromisesRequest);
    }

    public string WhereClause { get; set; }
    public int PageSize { get; set; } = 100;
    public string ContinuationToken { get; set; }
}

public class ListResolutionPromisesResponse
{
    public List<ResolutionPromiseResult> Results { get; set; } = [];
    public string ContinuationToken { get; set; }
    public bool MoreResultsAvailable { get; set; }
    public int ResultCount { get; set; }
}

public class GetResolutionPromisesRequest : DataHubCLIRequest<GetResolutionPromisesResponse>
{
    public GetResolutionPromisesRequest()
    {
        RequestType = nameof(GetResolutionPromisesRequest);
    }

    public List<string> PromiseIds { get; set; } = [];
}

public class GetResolutionPromisesResponse
{
    public List<ResolutionPromiseResult> Results { get; set; } = [];
    public int ResultCount { get; set; }
}

public class DeleteResolutionPromisesRequest : DataHubCLIRequest<DeleteResolutionPromisesResponse>
{
    public DeleteResolutionPromisesRequest()
    {
        RequestType = nameof(DeleteResolutionPromisesRequest);
    }

    public List<string> PromiseIds { get; set; } = [];
    public string WhereClause { get; set; }
    public int PageSize { get; set; } = 1000;
    public string ContinuationToken { get; set; }
    public bool DryRun { get; set; }
}

public class DeleteResolutionPromisesResponse
{
    public int MatchedCount { get; set; }
    public int DeletedCount { get; set; }
    public int FailedCount { get; set; }
    public string ContinuationToken { get; set; }
    public bool MoreResultsAvailable { get; set; }
    public List<DeleteResolutionPromiseResult> Results { get; set; } = [];
}

public class PatchResolutionPromiseRequest : DataHubCLIRequest<PatchResolutionPromiseResponse>
{
    public PatchResolutionPromiseRequest()
    {
        RequestType = nameof(PatchResolutionPromiseRequest);
    }

    public string PromiseId { get; set; }
    public List<Patch> Operations { get; set; } = [];
    public bool DryRun { get; set; }
}

public class PatchResolutionPromiseResponse
{
    public string PromiseId { get; set; }
    public bool Success { get; set; }
    public bool Changed { get; set; }
    public string Status { get; set; }
    public string Reason { get; set; }
    public ResolutionPromiseResult Result { get; set; }
}

public class ResolutionPromiseResult
{
    public string PromiseId { get; set; }
    public string DataHubEntityType { get; set; }
    public string DataHubEntityId { get; set; }
    public string EntityReferencePath { get; set; }
    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public string SourceEntityId { get; set; }
    public string TargetEntityType { get; set; }
}

public class DeleteResolutionPromiseResult : ResolutionPromiseResult
{
    public string Status { get; set; }
    public string Reason { get; set; }
}
