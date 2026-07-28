using System;
using System.Linq;
using System.Net;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Client;

public sealed class DataHubClientException : Exception
{
    public DataHubClientException(
        HttpStatusCode statusCode,
        string reasonPhrase,
        string requestType,
        string correlationId,
        string responseBody,
        DataHubErrorResponse errorResponse = null,
        Exception innerException = null)
        : base(CreateMessage(statusCode, reasonPhrase, requestType, correlationId, errorResponse, responseBody), innerException)
    {
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        RequestType = requestType;
        CorrelationId = correlationId;
        ResponseBody = responseBody;
        ErrorResponse = errorResponse;
    }

    public HttpStatusCode StatusCode { get; }
    public string ReasonPhrase { get; }
    public string RequestType { get; }
    public string CorrelationId { get; }
    public string ResponseBody { get; }
    public DataHubErrorResponse ErrorResponse { get; }

    private static string CreateMessage(
        HttpStatusCode statusCode,
        string reasonPhrase,
        string requestType,
        string correlationId,
        DataHubErrorResponse errorResponse,
        string responseBody)
    {
        var message = errorResponse?.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = string.IsNullOrWhiteSpace(responseBody) ? reasonPhrase : responseBody;
        }

        var details = CreateDetailMessage(errorResponse);
        if (!string.IsNullOrWhiteSpace(details))
        {
            message = $"{message} Details: {details}";
        }

        return $"DataHub request '{requestType}' failed with HTTP {(int)statusCode} {reasonPhrase}. CorrelationId: {correlationId}. {message}";
    }

    private static string CreateDetailMessage(DataHubErrorResponse errorResponse)
    {
        if (errorResponse?.Details is not { Count: > 0 })
        {
            return null;
        }

        const int maxDetails = 5;
        var detailMessages = errorResponse.Details
            .Take(maxDetails)
            .Select(detail =>
            {
                var prefix = string.Join(
                    " ",
                    new[] { detail.Field, string.IsNullOrWhiteSpace(detail.Code) ? null : $"({detail.Code})" }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));

                return string.IsNullOrWhiteSpace(prefix)
                    ? detail.Message
                    : $"{prefix}: {detail.Message}";
            })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        var remainingCount = errorResponse.Details.Count - maxDetails;
        if (remainingCount > 0)
        {
            detailMessages.Add($"... and {remainingCount} more.");
        }

        return string.Join(" ", detailMessages);
    }
}
