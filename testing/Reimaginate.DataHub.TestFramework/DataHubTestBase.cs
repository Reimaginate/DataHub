using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.Config;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.TestFramework;

public class DataHubTestBase(
    Action<IConfigurationBuilder> configurationBuilder,
    Action<IServiceCollection, IConfiguration>? configureAgentServices = null,
    Func<AddDataHubServiceOptions, IConfiguration, AddDataHubServiceOptions>? dataHubOptions = null)
    : UnitTestBase(configurationBuilder, (services, config) =>
    {
        configureAgentServices?.Invoke(services, config);

        var dataHubHostServiceCollection = new ServiceCollection();
        if (dataHubOptions != null)
            dataHubHostServiceCollection.AddDataHub(cfg => dataHubOptions(cfg, config));

        var dataHubHostServices = dataHubHostServiceCollection.BuildServiceProvider();
        services.AddSingleton(new DataHubServices(dataHubHostServices));
    });

