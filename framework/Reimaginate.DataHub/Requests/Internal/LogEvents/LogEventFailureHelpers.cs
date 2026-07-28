using System.Collections.Generic;
using System.Linq;
using Reimaginate.DataHub.Diagnostics;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;

namespace Reimaginate.DataHub.Requests.Internal.LogEvents;

internal static class LogEventFailureHelpers
{
    public const string PersistenceFailureCode = "LOG_PERSISTENCE_FAILED";
    public const string NotificationFailureCode = "NOTIFICATION_DISPATCH_FAILED";

    public static LogEventsResponse CreatePersistenceFailureResponse(IEnumerable<DataAccessFailure<LogEntry>> failures)
    {
        var failureList = failures.ToList();
        foreach (var failureGroup in failureList.GroupBy(failure => failure.Item?.Type))
        {
            DataHubTelemetry.RecordPersistenceFailure("log_entry_write", failureGroup.Key, failureGroup.LongCount());
        }

        return new LogEventsResponse
        {
            Success = false,
            FailureReason = $"{PersistenceFailureCode}: {string.Join("\n", failureList.Select(CreateFailureMessage))}",
            Failures = failureList
        };
    }

    public static void ThrowIfFailed(LogEventsResponse response, string requestType)
    {
        if (response.Success)
        {
            return;
        }

        throw new DataHubException(
            DataHubErrorCategory.ServerError,
            response.FailureReason ?? PersistenceFailureCode,
            requestType,
            details: CreateDetails(response.Failures, PersistenceFailureCode));
    }

    public static DataHubException CreateNotificationDispatchException(string requestType, IEnumerable<string> failureMessages)
    {
        var messages = failureMessages.ToList();
        DataHubTelemetry.RecordNotificationDispatchFailure(requestType, messages.Count);
        return new DataHubException(
            DataHubErrorCategory.ServerError,
            $"{NotificationFailureCode}: log entries were persisted but one or more notifications failed to dispatch.",
            requestType,
            details: messages.Select((message, index) => new DataHubErrorDetail
            {
                Field = $"Notifications[{index}]",
                Code = NotificationFailureCode,
                Message = message
            }).ToList());
    }

    private static IReadOnlyCollection<DataHubErrorDetail> CreateDetails(
        IEnumerable<DataAccessFailure<LogEntry>> failures,
        string code)
    {
        return failures.Select(failure => new DataHubErrorDetail
        {
            Field = string.IsNullOrWhiteSpace(failure.Item?.id) ? null : $"LogEntry:{failure.Item.id}",
            Code = code,
            Message = failure.Error?.Message
        }).ToList();
    }

    private static string CreateFailureMessage(DataAccessFailure<LogEntry> failure)
    {
        var id = string.IsNullOrWhiteSpace(failure.Item?.id) ? "unknown" : failure.Item.id;
        return $"{id}: {failure.Error?.Message}";
    }
}
