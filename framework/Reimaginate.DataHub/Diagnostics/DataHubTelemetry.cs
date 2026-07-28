using System;
using System.Diagnostics.Metrics;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Diagnostics;

public static class DataHubTelemetry
{
    public const string MeterName = "Reimaginate.DataHub";
    public const string MeterVersion = "1.0.0";

    private static readonly Meter Meter = new(MeterName, MeterVersion);

    private static readonly Counter<long> RequestFailureCounter = Meter.CreateCounter<long>(
        "datahub.request.failure.count",
        description: "DataHub endpoint request failures by request type, category, and status code.");

    private static readonly Counter<long> DomainFailureCounter = Meter.CreateCounter<long>(
        "datahub.domain_failure.count",
        description: "DataHub domain failures by failure type and source.");

    private static readonly Counter<long> JobStatusCounter = Meter.CreateCounter<long>(
        "datahub.job.status.count",
        description: "DataHub job status transitions by status, type, and target.");

    private static readonly Counter<long> NotificationDispatchFailureCounter = Meter.CreateCounter<long>(
        "datahub.notification.dispatch.failure.count",
        description: "DataHub notification dispatch failures by notification type.");

    private static readonly Counter<long> ProcessingLockTimeoutCounter = Meter.CreateCounter<long>(
        "datahub.processing_lock.timeout.count",
        description: "DataHub processing lock timeout or acquisition failures.");

    private static readonly Counter<long> PersistenceFailureCounter = Meter.CreateCounter<long>(
        "datahub.persistence.failure.count",
        description: "DataHub persistence failures by operation and document type.");

    public static void RecordRequestFailure(DataHubErrorResponse errorResponse, int statusCode)
    {
        ArgumentNullException.ThrowIfNull(errorResponse);

        RequestFailureCounter.Add(
            1,
            new("request.type", Safe(errorResponse.RequestType)),
            new("error.category", Safe(errorResponse.Category)),
            new("http.status_code", statusCode));
    }

    public static void RecordDomainFailure(string failureType, string source = null, long count = 1)
    {
        DomainFailureCounter.Add(
            count,
            new("failure.type", Safe(failureType)),
            new("source", Safe(source)));
    }

    public static void RecordJobStatus(string status, string jobType = null, string target = null, long count = 1)
    {
        JobStatusCounter.Add(
            count,
            new("job.status", Safe(status)),
            new("job.type", Safe(jobType)),
            new("job.target", Safe(target)));
    }

    public static void RecordNotificationDispatchFailure(string notificationType, long count = 1)
    {
        NotificationDispatchFailureCounter.Add(
            count,
            [new("notification.type", Safe(notificationType))]);

        RecordDomainFailure("notification_dispatch", notificationType, count);
    }

    public static void RecordProcessingLockTimeout(string operation = null, long count = 1)
    {
        ProcessingLockTimeoutCounter.Add(
            count,
            [new("operation", Safe(operation))]);
    }

    public static void RecordPersistenceFailure(string operation, string documentType = null, long count = 1)
    {
        PersistenceFailureCounter.Add(
            count,
            new("operation", Safe(operation)),
            new("document.type", Safe(documentType)));
    }

    private static string Safe(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }
}
