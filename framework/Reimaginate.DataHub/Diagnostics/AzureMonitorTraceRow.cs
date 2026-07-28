using System;

namespace Reimaginate.DataHub.Diagnostics;

public class AzureMonitorTraceRow
{
    public DateTimeOffset? StartTimeUtc { get; set; }
    public string ItemType { get; set; }
    public string OperationId { get; set; }
    public string SpanId { get; set; }
    public string ParentSpanId { get; set; }
    public string Name { get; set; }
    public string Message { get; set; }
    public double? DurationMs { get; set; }
    public bool? Success { get; set; }
    public string Severity { get; set; }
    public string CloudRoleName { get; set; }
    public string RequestType { get; set; }
    public string ErrorId { get; set; }
    public string ErrorCategory { get; set; }
    public int? HttpStatusCode { get; set; }
    public string TagsJson { get; set; }
    public string DetailsJson { get; set; }
}
