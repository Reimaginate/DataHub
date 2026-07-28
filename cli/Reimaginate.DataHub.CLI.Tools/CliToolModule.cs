using System.CommandLine;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.CLI.Base.Dynamic;
using Reimaginate.DataHub.CLI.Tools.Config;

namespace Reimaginate.DataHub.CLI.Tools;

public sealed class CliToolModule : ICliToolModule
{
    public string PackageId => "Reimaginate.DataHub.CLI.Tools";
    public string ModuleName => "DataHub";
    public string? Version => typeof(CliToolModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDataHubCliCommands(configuration);
        CliToolAssemblyRegistration.RegisterMediatorHandlers(services, typeof(CliToolModule).Assembly);
    }

    public void ConfigureCommands(RootCommand rootCommand, IServiceProvider serviceProvider)
        => rootCommand.AddDataHubTools(serviceProvider);
}
