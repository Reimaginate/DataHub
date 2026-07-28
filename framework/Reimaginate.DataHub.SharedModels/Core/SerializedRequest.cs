namespace Reimaginate.DataHub.SharedModels.Core;

public class SerializedRequest
{
    public string RequestType { get; set; }
    public string CorrelationId { get; set; }
    public string Data { get; set; }
    public DataHubTraceOptions TraceOptions { get; set; }
}
