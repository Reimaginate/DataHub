using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Update;

public class UpdateCommand(IServiceProvider serviceProvider) : TopLevelCommand("update", serviceProvider);