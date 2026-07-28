using System;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Exceptions;

public class DataHubException : Exception
{
    public DataHubException(
        string category,
        string message,
        string requestType = null,
        string correlationId = null,
        IReadOnlyCollection<DataHubErrorDetail> details = null,
        Exception innerException = null)
        : base(message, innerException)
    {
        Category = category;
        RequestType = requestType;
        CorrelationId = correlationId;
        Details = details ?? Array.Empty<DataHubErrorDetail>();
    }

    public string Category { get; }
    public string RequestType { get; }
    public string CorrelationId { get; }
    public IReadOnlyCollection<DataHubErrorDetail> Details { get; }
}
