using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Options;
using Reimaginate.DataHub.Config;
using Reimaginate.DataHub.Diagnostics;
using Reimaginate.DataHub.Requests.Internal.DispatchNotifications;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Notifications;
using Reimaginate.Mapper;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.DispatchJobs;

public class DispatchJobsRequestHandler : IHandler<DispatchJobsRequest, DispatchJobsResponse>
{
    private readonly IMapper _mapper;
    private readonly IMediator _mediator;
    private readonly ITimeService _timeService;
    private readonly EventGridPublisherClient _eventGridClient;

    public DispatchJobsRequestHandler(IOptions<NotificationServiceOptions> options, IMediator mediator, IMapper mapper, ITimeService timeService)
    {
        _mediator = mediator;
        _mapper = mapper;
        _timeService = timeService;
        var azureEventGridConfig = options.Value.AzureEventGridClientOptions;
        if (!string.IsNullOrEmpty(azureEventGridConfig.EventGridUrl))
        {
            _eventGridClient = !string.IsNullOrEmpty(azureEventGridConfig.EventGridUrl) && !string.IsNullOrEmpty(azureEventGridConfig.EventGridKey) ? new EventGridPublisherClient(new Uri(azureEventGridConfig.EventGridUrl), new AzureKeyCredential(azureEventGridConfig.EventGridKey)) : null;
        }
    }

    public async Task<DispatchJobsResponse> HandleAsync(DispatchJobsRequest request, CancellationToken cancellationToken)
    {
        if (_eventGridClient == null)
        {
            DataHubTelemetry.RecordNotificationDispatchFailure(nameof(JobNotification), request.Jobs?.Count ?? 0);
            return new DispatchJobsResponse()
            {
                Success = false,
                FailureReason = "EVENT_GRID_NOT_CONFIGURED"
            };
        }

        try
        {
            var resultsDic = request.Jobs.ToDictionary(x => x.JobId, v => (DispatchJobResult)null);

            foreach (var job in request.Jobs)
            {
                var notification = new JobNotification()
                {
                    Subject = job.JobId,
                    JobId = job.JobId,
                    JobType = job.Type,
                    JobTarget = job.Target
                };

                var dispatchNotificationsResponse = (await _mediator.TrySend(new DispatchNotificationsRequest([notification]), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                resultsDic[job.JobId] = new DispatchJobResult()
                {
                    Success = dispatchNotificationsResponse.Success,
                    FailureReason = dispatchNotificationsResponse.Failures != null ? "FAILED_TO_DISPATCH_NOTIFICATION: " + string.Join("\n", dispatchNotificationsResponse.Failures.Select(s => s.Exception.Message)) : null,
                    Result = job
                };

                if (!dispatchNotificationsResponse.Success)
                {
                    DataHubTelemetry.RecordNotificationDispatchFailure(nameof(JobNotification));
                }
            }

            return new DispatchJobsResponse()
            {
                Success = true,
                Results = resultsDic.Values.ToList()
            };
        }
        catch (Exception ex)
        {
            return new DispatchJobsResponse()
            {
                Success = false,
                FailureReason = ex.Message,
            };
        }
    }
}
