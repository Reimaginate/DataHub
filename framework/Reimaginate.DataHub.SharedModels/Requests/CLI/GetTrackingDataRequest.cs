using System;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetTrackingDataRequest : DataHubCLIRequest<GetTrackingDataResponse>
{
    public GetTrackingDataRequest()
    {
        RequestType = nameof(GetTrackingDataRequest);
    }
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
    public int? PageSize { get; set; }
    public DateTimeOffset? FromDateTime { get; set; }
    public DateTimeOffset? ToDateTime { get; set; }
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; } = false;
}