using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Disable;

public class DisableCommand(IServiceProvider serviceProvider) : TopLevelCommand("disable", serviceProvider);

