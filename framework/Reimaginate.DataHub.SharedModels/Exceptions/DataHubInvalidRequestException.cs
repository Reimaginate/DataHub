using System;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Exceptions;

public sealed class DataHubInvalidRequestException : DataHubException
{
    public DataHubInvalidRequestException(
        string message,
        string requestType = null,
        string correlationId = null,
        IReadOnlyCollection<DataHubErrorDetail> details = null,
        Exception innerException = null)
        : base(DataHubErrorCategory.InvalidRequest, message, requestType, correlationId, details, innerException)
    {
    }
}
