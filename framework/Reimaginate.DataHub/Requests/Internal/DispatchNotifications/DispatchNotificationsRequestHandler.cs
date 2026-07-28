using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Reimaginate.DataHub.Config;
using Reimaginate.DataHub.Diagnostics;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DispatchNotifications;

public class DispatchNotificationsRequestHandler : IHandler<DispatchNotificationsRequest, DispatchNotificationsResponse>
{
    private readonly NotificationServiceOptions _options;
    private readonly EventGridPublisherClient _eventGridClient;


    public DispatchNotificationsRequestHandler(IOptions<NotificationServiceOptions> options)
    {
        _options = options.Value;

        if (_options.UseMessagingService != "AzureEventGrid") return;

        var azureEventGridConfig = _options.AzureEventGridClientOptions;
        if (!string.IsNullOrEmpty(azureEventGridConfig.EventGridUrl))
        {
            _eventGridClient = !string.IsNullOrEmpty(azureEventGridConfig.EventGridUrl) && !string.IsNullOrEmpty(azureEventGridConfig.EventGridKey) ? new EventGridPublisherClient(new Uri(azureEventGridConfig.EventGridUrl), new AzureKeyCredential(azureEventGridConfig.EventGridKey)) : null;
        }

    }

    public async Task<DispatchNotificationsResponse> HandleAsync(DispatchNotificationsRequest request, CancellationToken cancellationToken)
    {
        if (_options.UseMessagingService != "AzureEventGrid")
        {
            return new DispatchNotificationsResponse()
            {
                Success = true
            };
        }

        if (_eventGridClient == null) throw new Exception("EVENT_GRID_NOT_CONFIGURED");

        var eventGridNotifications = request.Notifications.Select(r => new EventGridEvent(
            r.Subject,
            r.EventType,
            "0",
            new BinaryData(JsonConvert.SerializeObject(r)))
        ).ToList();


        var eventsToProcess = new List<EventGridEvent>(eventGridNotifications);
        var failures = new List<DispatchNotificationFailure>();

        while (eventsToProcess.Any())
        {
            var batch = eventsToProcess.Take(100).ToList();

            var response = await _eventGridClient.SendEventsAsync(batch, cancellationToken);
            if (response.Status != 200)
            {
                failures.AddRange(batch.Select(ev =>
                {
                    var i = eventGridNotifications.IndexOf(ev);
                    var notification = request.Notifications[i];
                    return new DispatchNotificationFailure(notification, new Exception(response.ReasonPhrase));
                }));
            }

            eventsToProcess.RemoveRange(0, batch.Count);
        }

        if (failures.Any())
        {
            foreach (var failureGroup in failures.GroupBy(failure => failure.Notification?.EventType))
            {
                DataHubTelemetry.RecordNotificationDispatchFailure(failureGroup.Key, failureGroup.LongCount());
            }

            return new DispatchNotificationsResponse()
            {
                Success = false,
                Failures = failures
            };
        }

        return new DispatchNotificationsResponse()
        {
            Success = true
        };
    }
}
