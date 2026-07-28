using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Reimaginate.CLI.Base.Abstractions;
using Reimaginate.CLI.Base.Config;
using Reimaginate.CLI.Base.Dynamic;
using Reimaginate.CLI.Base.Profiles;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Auth;
using Reimaginate.DataHub.CLI.Tools.Shared.Contexts;
using Reimaginate.DataHub.CLI.Tools.Shared.Models;
using Reimaginate.DataHub.CLI.Tools.Shared.Requests;
using Reimaginate.DataHub.CLI.Tools.Shared.Services.Connections;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.CLI.Tools.Config;

public static class ConfigureServices
{
    public static IServiceCollection AddDataHubCliCommands(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(config);
        services.AddBaseCommands(config);
        services.Configure<AppSettings>(config.GetSection("AppConfig"));
        services.AddHttpClient();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IToolProfileDescriptor, DataHubContextDescriptor>());
        services.TryAddSingleton<IDataHubCliTokenProvider, MsalDataHubCliTokenProvider>();
        services.TryAddSingleton<IConnectionsService, ConnectionsService>();
        services.TryAddSingleton<IDataHubCliClient, DataHubCliClient>();
        services.TryAddSingleton<ICLIApi, DataHubCliApi>();
        services.TryAddSingleton<IMediator, ReflectionMediator>();
        CliToolAssemblyRegistration.RegisterMediatorHandlers(services, typeof(ConfigureServices).Assembly);

        RegisterCommands(services);
        return services;
    }

    private static void RegisterCommands(IServiceCollection services)
    {
        var marker = typeof(ConfigureServices);

        var topLevelCommandTypes = marker
            .Assembly
            .GetExportedTypes()
            .Where(type => typeof(TopLevelCommand).IsAssignableFrom(type) && type is { IsAbstract: false })
            .ToList();

        foreach (var commandType in topLevelCommandTypes)
        {
            services.TryAdd(ServiceDescriptor.Singleton(commandType, commandType));
        }

        var subCommandTypes = marker
            .Assembly
            .GetExportedTypes()
            .Where(type => !type.IsAbstract &&
                           (type.BaseType?.IsGenericType ?? false) &&
                           type.BaseType.GetGenericTypeDefinition() == typeof(Reimaginate.DataHub.CLI.Tools.PluginBase.SubCommand<>))
            .ToList();

        foreach (var subCommandType in subCommandTypes)
        {
            foreach (var serviceType in GetSubCommandServiceTypes(subCommandType))
            {
                if (!services.Any(service => service.ServiceType == serviceType &&
                                             service.ImplementationType == subCommandType))
                {
                    services.AddSingleton(serviceType, subCommandType);
                }
            }
        }
    }

    private static IEnumerable<Type> GetSubCommandServiceTypes(Type subCommandType)
    {
        var compatibilityBase = subCommandType.BaseType;
        if (compatibilityBase == null)
        {
            yield break;
        }

        yield return compatibilityBase;

        var frameworkBase = compatibilityBase.BaseType;
        if (frameworkBase?.IsGenericType == true &&
            frameworkBase.GetGenericTypeDefinition() == typeof(SubCommand<>))
        {
            yield return frameworkBase;
        }
    }
}
