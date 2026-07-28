using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.ProcessingLockService;

namespace Reimaginate.DataHub.Client.Config;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddDataHubClient(this IServiceCollection services, Action<AddDataHubClientOptions> options = null)
    {
        var dataHubClientOptions = new AddDataHubClientOptions();
        options?.Invoke(dataHubClientOptions);

        services.AddScoped<IDataHubClient>(cfg => new DataHubClient(cfg.GetRequiredService<HttpClient>(), dataHubClientOptions.DataHubClientOptions));
        return services;
    }
}