using Microsoft.Extensions.DependencyInjection;

namespace Reimaginate.DataHub.TestFramework;

public class DataHubServices(ServiceProvider services)
{
    public ServiceProvider Services { get; set; } = services;
}