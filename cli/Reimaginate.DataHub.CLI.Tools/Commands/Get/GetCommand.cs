using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get;

public class GetCommand(IServiceProvider serviceProvider) : TopLevelCommand("get", serviceProvider);

