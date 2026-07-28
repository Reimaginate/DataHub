using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Enable;

public class EnableCommand(IServiceProvider serviceProvider) : TopLevelCommand("enable", serviceProvider);

