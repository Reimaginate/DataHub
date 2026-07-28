using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reimaginate.DataHub.Config;
using Reimaginate.ProcessingLockService;

namespace Reimaginate.DataHub.AspNetCore.Readiness;

public static class DataHubReadinessEndpointExtensions
{
    public static RouteHandlerBuilder MapDataHubReadinessEndpoint(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/readyz",
        Action<DataHubReadinessOptions> configure = null)
    {
        var options = new DataHubReadinessOptions();
        configure?.Invoke(options);
        var cache = new ReadinessCache();

        return endpoints.MapGet(pattern, async (
            HttpContext httpContext,
            IConfiguration configuration,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            var now = DateTimeOffset.UtcNow;
            if (cache.TryGet(now, options.CacheDuration, out var cached))
            {
                return CreateResult(cached);
            }

            var result = await CheckAsync(options, configuration, services, cancellationToken);
            cache.Set(now, result);
            return CreateResult(result);
        });
    }

    private static async Task<ReadinessResult> CheckAsync(
        DataHubReadinessOptions options,
        IConfiguration configuration,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var checks = new List<ReadinessCheckResult>();
        await CheckCosmosAsync(options, configuration, services, checks, cancellationToken);
        await CheckProcessingLocksAsync(options, configuration, services, checks, cancellationToken);
        CheckEventGrid(options, services, checks);

        return new ReadinessResult
        {
            Status = checks.All(check => check.Status == "Healthy") ? "Healthy" : "Unhealthy",
            Service = options.ServiceName,
            CheckedOn = DateTimeOffset.UtcNow,
            Checks = checks
        };
    }

    private static async Task CheckCosmosAsync(
        DataHubReadinessOptions options,
        IConfiguration configuration,
        IServiceProvider services,
        List<ReadinessCheckResult> checks,
        CancellationToken cancellationToken)
    {
        var useDatabase = configuration["DataHub:DataStoreOptions:UseDatabase"];
        if (!string.Equals(useDatabase, "AzureCosmosDb", StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(ReadinessCheckResult.Healthy("cosmos", "Cosmos check skipped because DataHub is not configured for AzureCosmosDb."));
            return;
        }

        if (!options.CheckCosmosConnectivity)
        {
            checks.Add(ReadinessCheckResult.Healthy("cosmos", "Cosmos configuration check enabled; connectivity probe disabled."));
            return;
        }

        try
        {
            var cosmosClient = services.GetRequiredService<CosmosClient>();
            cancellationToken.ThrowIfCancellationRequested();
            _ = await cosmosClient.ReadAccountAsync();
            checks.Add(ReadinessCheckResult.Healthy("cosmos", "Cosmos account is reachable."));
        }
        catch (Exception ex)
        {
            checks.Add(ReadinessCheckResult.Unhealthy("cosmos", ex.Message));
        }
    }

    private static async Task CheckProcessingLocksAsync(
        DataHubReadinessOptions options,
        IConfiguration configuration,
        IServiceProvider services,
        List<ReadinessCheckResult> checks,
        CancellationToken cancellationToken)
    {
        if (!options.CheckProcessingLocks)
        {
            checks.Add(ReadinessCheckResult.Healthy("processing-locks", "Processing lock check disabled."));
            return;
        }

        var repository = configuration["DataHub:ProcessingLockOptions:UseRepository"];
        if (!string.Equals(repository, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(ReadinessCheckResult.Healthy("processing-locks", "Processing lock check skipped because Redis is not configured."));
            return;
        }

        try
        {
            var processingLockService = services.GetRequiredService<IProcessingLockService>();
            var response = await processingLockService.GetLocksAsync(cancellationToken);
            response.ThrowIfUnsuccessful();
            checks.Add(ReadinessCheckResult.Healthy("processing-locks", "Processing lock repository is reachable."));
        }
        catch (Exception ex)
        {
            checks.Add(ReadinessCheckResult.Unhealthy("processing-locks", ex.Message));
        }
    }

    private static void CheckEventGrid(
        DataHubReadinessOptions options,
        IServiceProvider services,
        List<ReadinessCheckResult> checks)
    {
        if (!options.CheckEventGridConfiguration)
        {
            checks.Add(ReadinessCheckResult.Healthy("event-grid", "Event Grid configuration check disabled."));
            return;
        }

        var notificationOptions = services.GetService<IOptions<NotificationServiceOptions>>()?.Value;
        if (!string.Equals(notificationOptions?.UseMessagingService, "AzureEventGrid", StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(ReadinessCheckResult.Healthy("event-grid", "Event Grid check skipped because AzureEventGrid is not configured."));
            return;
        }

        var eventGridOptions = notificationOptions.AzureEventGridClientOptions;
        if (eventGridOptions == null ||
            string.IsNullOrWhiteSpace(eventGridOptions.EventGridUrl) ||
            string.IsNullOrWhiteSpace(eventGridOptions.EventGridKey))
        {
            checks.Add(ReadinessCheckResult.Unhealthy("event-grid", "AzureEventGrid is selected but EventGridUrl or EventGridKey is missing."));
            return;
        }

        checks.Add(ReadinessCheckResult.Healthy("event-grid", "Event Grid configuration is present."));
    }

    private static IResult CreateResult(ReadinessResult result)
    {
        return result.Status == "Healthy"
            ? Results.Ok(result)
            : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private sealed class ReadinessCache
    {
        private readonly object _gate = new();
        private DateTimeOffset _createdOn;
        private ReadinessResult _result;

        public bool TryGet(DateTimeOffset now, TimeSpan duration, out ReadinessResult result)
        {
            lock (_gate)
            {
                if (_result != null && now - _createdOn < duration)
                {
                    result = _result;
                    return true;
                }
            }

            result = null;
            return false;
        }

        public void Set(DateTimeOffset createdOn, ReadinessResult result)
        {
            lock (_gate)
            {
                _createdOn = createdOn;
                _result = result;
            }
        }
    }

    private sealed class ReadinessResult
    {
        public string Status { get; set; }
        public string Service { get; set; }
        public DateTimeOffset CheckedOn { get; set; }
        public List<ReadinessCheckResult> Checks { get; set; } = [];
    }

    private sealed class ReadinessCheckResult
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public string Description { get; set; }

        public static ReadinessCheckResult Healthy(string name, string description)
        {
            return new ReadinessCheckResult { Name = name, Status = "Healthy", Description = description };
        }

        public static ReadinessCheckResult Unhealthy(string name, string description)
        {
            return new ReadinessCheckResult { Name = name, Status = "Unhealthy", Description = description };
        }
    }
}
