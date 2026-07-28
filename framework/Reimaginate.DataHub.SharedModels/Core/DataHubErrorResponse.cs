using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Core;

public sealed class DataHubErrorResponse
{
    public string ErrorId { get; set; }
    public string CorrelationId { get; set; }
    public string RequestType { get; set; }
    public string Category { get; set; }
    public string Message { get; set; }
    public List<DataHubErrorDetail> Details { get; set; } = new();
}
