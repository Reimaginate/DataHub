using System;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetTraceRequest : DataHubCLIRequest<GetTraceResponse>
{
    public GetTraceRequest()
    {
        RequestType = nameof(GetTraceRequest);
    }

    public string TraceCorrelationId { get; set; }
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    public int? LookbackHours { get; set; }
    public int? Limit { get; set; }
    public bool IncludeLogs { get; set; }
    public bool IncludePayloadDetails { get; set; }
}
