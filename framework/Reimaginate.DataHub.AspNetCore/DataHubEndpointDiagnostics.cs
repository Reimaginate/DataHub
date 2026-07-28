using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Reimaginate.DataHub.AspNetCore.Observability;
using Reimaginate.DataHub.Diagnostics;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;

namespace Reimaginate.DataHub.AspNetCore;

public static class DataHubEndpointDiagnostics
{
    public const string CorrelationIdHeaderName = "x-correlation-id";

    public static IResult CreateErrorResult(
        Exception exception,
        HttpContext httpContext,
        ILogger logger,
        string requestType = null,
        string correlationId = null)
    {
        var exceptionCorrelationId = exception is DataHubException dataHubException ? dataHubException.CorrelationId : null;
        var errorResponse = CreateErrorResponse(exception, requestType, correlationId ?? exceptionCorrelationId ?? ResolveCorrelationId(httpContext));
        var statusCode = GetStatusCode(errorResponse.Category);

        httpContext.Response.Headers[CorrelationIdHeaderName] = errorResponse.CorrelationId;
        DataHubEndpointObservability.ApplyError(errorResponse, statusCode);
        DataHubTelemetry.RecordRequestFailure(errorResponse, statusCode);
        RecordLockFailureIfDetected(exception, requestType ?? errorResponse.RequestType);
        LogException(logger, exception, statusCode, errorResponse);

        return Results.Text(
            JsonConvert.SerializeObject(errorResponse),
            "application/json",
            Encoding.UTF8,
            statusCode);
    }

    public static DataHubErrorResponse CreateErrorResponse(
        Exception exception,
        string requestType = null,
        string correlationId = null)
    {
        var errorId = Guid.NewGuid().ToString("N");
        var exceptionCorrelationId = exception is DataHubException dataHubException ? dataHubException.CorrelationId : null;
        var resolvedCorrelationId = ResolveCorrelationId(correlationId ?? exceptionCorrelationId);
        var category = ResolveCategory(exception);
        var safeMessage = CreateSafeMessage(exception, category);

        return new DataHubErrorResponse
        {
            ErrorId = errorId,
            CorrelationId = resolvedCorrelationId,
            RequestType = ResolveRequestType(exception, requestType),
            Category = category,
            Message = safeMessage,
            Details = CreateDetails(exception).ToList()
        };
    }

    public static IResult CreateUnauthorizedResult(HttpContext httpContext, ILogger logger, string requestType = null, string correlationId = null)
    {
        return CreateErrorResult(
            new DataHubException(DataHubErrorCategory.Unauthorized, "Unauthorized.", requestType, correlationId),
            httpContext,
            logger,
            requestType,
            correlationId);
    }

    public static string ResolveCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var headerValue) &&
            !string.IsNullOrWhiteSpace(headerValue.FirstOrDefault()))
        {
            return headerValue.First();
        }

        return ResolveCorrelationId((string)null);
    }

    private static string ResolveCorrelationId(string correlationId)
    {
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        var activityTraceId = Activity.Current?.TraceId.ToString();
        return string.IsNullOrWhiteSpace(activityTraceId) ? Guid.NewGuid().ToString("N") : activityTraceId;
    }

    private static string ResolveCategory(Exception exception)
    {
        if (exception is DataHubException dataHubException)
        {
            return dataHubException.Category;
        }

        if (exception is ValidationException)
        {
            return DataHubErrorCategory.ValidationFailed;
        }

        if (string.Equals(exception.Message, "Validation failure: Not Authorized", StringComparison.OrdinalIgnoreCase))
        {
            return DataHubErrorCategory.Unauthorized;
        }

        if (exception.Message.StartsWith("Validation failure:", StringComparison.OrdinalIgnoreCase))
        {
            return DataHubErrorCategory.ValidationFailed;
        }

        return DataHubErrorCategory.ServerError;
    }

    private static string ResolveRequestType(Exception exception, string requestType)
    {
        if (!string.IsNullOrWhiteSpace(requestType))
        {
            return requestType;
        }

        return exception is DataHubException dataHubException ? dataHubException.RequestType : null;
    }

    private static string CreateSafeMessage(Exception exception, string category)
    {
        if (exception is DataHubException dataHubException)
        {
            return dataHubException.Message;
        }

        if (category == DataHubErrorCategory.ServerError)
        {
            return "An unexpected DataHub server error occurred.";
        }

        if (exception is ValidationException)
        {
            return "Request validation failed.";
        }

        return exception.Message;
    }

    private static IEnumerable<DataHubErrorDetail> CreateDetails(Exception exception)
    {
        if (exception is DataHubException dataHubException)
        {
            return dataHubException.Details;
        }

        if (exception is ValidationException validationException)
        {
            return validationException.Errors.Select(error => new DataHubErrorDetail
            {
                Field = error.PropertyName,
                Code = error.ErrorCode,
                Message = error.ErrorMessage
            });
        }

        if (exception.Message.StartsWith("Validation failure:", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new DataHubErrorDetail
                {
                    Code = DataHubErrorCategory.ValidationFailed,
                    Message = exception.Message["Validation failure:".Length..].Trim()
                }
            };
        }

        return Array.Empty<DataHubErrorDetail>();
    }

    private static int GetStatusCode(string category)
    {
        return category switch
        {
            DataHubErrorCategory.Unauthorized => StatusCodes.Status401Unauthorized,
            DataHubErrorCategory.InvalidRequest => StatusCodes.Status400BadRequest,
            DataHubErrorCategory.DeserializationFailed => StatusCodes.Status400BadRequest,
            DataHubErrorCategory.ValidationFailed => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static void LogException(ILogger logger, Exception exception, int statusCode, DataHubErrorResponse errorResponse)
    {
        if (statusCode >= 500)
        {
            logger.LogError(
                exception,
                "DataHub request failed. StatusCode: {StatusCode}; Category: {Category}; RequestType: {RequestType}; CorrelationId: {CorrelationId}; ErrorId: {ErrorId}",
                statusCode,
                errorResponse.Category,
                errorResponse.RequestType,
                errorResponse.CorrelationId,
                errorResponse.ErrorId);
            return;
        }

        logger.LogWarning(
            exception,
            "DataHub request was rejected. StatusCode: {StatusCode}; Category: {Category}; RequestType: {RequestType}; CorrelationId: {CorrelationId}; ErrorId: {ErrorId}",
            statusCode,
            errorResponse.Category,
            errorResponse.RequestType,
            errorResponse.CorrelationId,
            errorResponse.ErrorId);
    }

    private static void RecordLockFailureIfDetected(Exception exception, string requestType)
    {
        var message = exception?.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (message.Contains("lock", StringComparison.OrdinalIgnoreCase) &&
            (message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("timed out", StringComparison.OrdinalIgnoreCase)))
        {
            DataHubTelemetry.RecordProcessingLockTimeout(requestType);
        }
    }
}
