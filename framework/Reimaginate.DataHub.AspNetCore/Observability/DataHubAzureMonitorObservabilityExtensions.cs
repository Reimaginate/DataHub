using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Reimaginate.DataHub.Diagnostics;

namespace Reimaginate.DataHub.AspNetCore.Observability;

public static class DataHubAzureMonitorObservabilityExtensions
{
    public const string DefaultConfigurationSection = "DataHub:Observability";

    public static IServiceCollection AddDataHubAzureMonitorObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = DefaultConfigurationSection,
        Action<DataHubObservabilityOptions> configure = null)
    {
        var options = new DataHubObservabilityOptions();
        configuration.GetSection(sectionName).Bind(options);
        options.ConnectionString = FirstNonEmpty(
            options.ConnectionString,
            configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]);
        configure?.Invoke(options);

        return services.AddDataHubAzureMonitorObservability(options);
    }

    public static IServiceCollection AddDataHubAzureMonitorObservability(
        this IServiceCollection services,
        Action<DataHubObservabilityOptions> configure = null)
    {
        var options = new DataHubObservabilityOptions();
        configure?.Invoke(options);

        return services.AddDataHubAzureMonitorObservability(options);
    }

    public static IServiceCollection AddDataHubAzureMonitorObservability(
        this IServiceCollection services,
        DataHubObservabilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddOptions<DataHubObservabilityOptions>().Configure(configured =>
        {
            Copy(options, configured);
        });

        if (!options.IsEnabled)
        {
            return services;
        }

        services.AddLogging(logging =>
        {
            logging.AddFilter((_, level) => level >= options.MinimumLogLevel);
        });

        services.Configure<OpenTelemetryLoggerOptions>(logging =>
        {
            logging.IncludeScopes = true;
            logging.IncludeFormattedMessage = false;
        });

        services.AddOpenTelemetry().UseAzureMonitor(azureMonitor =>
        {
            if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                azureMonitor.ConnectionString = options.ConnectionString;
            }

            azureMonitor.EnableLiveMetrics = options.EnableLiveMetrics;
            azureMonitor.TracesPerSecond = options.TracesPerSecond;
            if (options.SamplingRatio.HasValue)
            {
                azureMonitor.SamplingRatio = options.SamplingRatio.Value;
            }
        });

        services.ConfigureOpenTelemetryTracerProvider((_, tracing) =>
        {
            tracing.ConfigureResource(resource => ConfigureResource(resource, options));
        });

        services.ConfigureOpenTelemetryMeterProvider((_, metrics) =>
        {
            metrics
                .ConfigureResource(resource => ConfigureResource(resource, options))
                .AddMeter(
                    DataHubTelemetry.MeterName,
                    "System.Runtime",
                    "Microsoft.AspNetCore.Hosting",
                    "System.Net.Http");
        });

        return services;
    }

    public static IServiceCollection AddDataHubJobAzureMonitorObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = DefaultConfigurationSection,
        Action<DataHubObservabilityOptions> configure = null)
    {
        return services.AddDataHubAzureMonitorObservability(configuration, sectionName, configure);
    }

    public static IServiceCollection AddDataHubJobAzureMonitorObservability(
        this IServiceCollection services,
        Action<DataHubObservabilityOptions> configure = null)
    {
        return services.AddDataHubAzureMonitorObservability(configure);
    }

    private static ResourceBuilder ConfigureResource(ResourceBuilder resource, DataHubObservabilityOptions options)
    {
        var attributes = new List<KeyValuePair<string, object>>();
        if (!string.IsNullOrWhiteSpace(options.EnvironmentName))
        {
            attributes.Add(new KeyValuePair<string, object>("deployment.environment.name", options.EnvironmentName));
        }

        if (!string.IsNullOrWhiteSpace(options.DeploymentId))
        {
            attributes.Add(new KeyValuePair<string, object>("service.instance.id", options.DeploymentId));
        }

        return resource
            .AddService(
                serviceName: string.IsNullOrWhiteSpace(options.CloudRoleName) ? "DataHub" : options.CloudRoleName,
                serviceVersion: string.IsNullOrWhiteSpace(options.ServiceVersion) ? null : options.ServiceVersion)
            .AddAttributes(attributes);
    }

    private static void Copy(DataHubObservabilityOptions source, DataHubObservabilityOptions target)
    {
        target.Profile = source.Profile;
        target.CloudRoleName = source.CloudRoleName;
        target.ServiceVersion = source.ServiceVersion;
        target.EnvironmentName = source.EnvironmentName;
        target.DeploymentId = source.DeploymentId;
        target.ConnectionString = source.ConnectionString;
        target.TracesPerSecond = source.TracesPerSecond;
        target.SamplingRatio = source.SamplingRatio;
        target.EnableLiveMetrics = source.EnableLiveMetrics;
        target.MinimumLogLevel = source.MinimumLogLevel;
        target.EnableRequestPayloadTracing = source.EnableRequestPayloadTracing;
        target.EnableResponsePayloadTracing = source.EnableResponsePayloadTracing;
        target.EnableUnsafeMediatorPayloadTags = source.EnableUnsafeMediatorPayloadTags;
        target.AllowCallerRequestedTracing = source.AllowCallerRequestedTracing;
        target.AllowCallerRequestedPayloadTracing = source.AllowCallerRequestedPayloadTracing;
        target.MaxCallerTraceDurationMinutes = source.MaxCallerTraceDurationMinutes;
        target.DiagnosticExpiresOn = source.DiagnosticExpiresOn;
        target.DiagnosticCorrelationIds = source.DiagnosticCorrelationIds?.ToList() ?? [];
        target.DiagnosticRequestTypes = source.DiagnosticRequestTypes?.ToList() ?? [];
        target.MaxTelemetryPayloadCharacters = source.MaxTelemetryPayloadCharacters;
        target.RedactedPropertyNames = source.RedactedPropertyNames?.ToList() ?? [];
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
