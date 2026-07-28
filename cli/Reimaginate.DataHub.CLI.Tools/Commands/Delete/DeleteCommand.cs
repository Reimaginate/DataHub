using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Delete;

public class DeleteCommand(IServiceProvider serviceProvider) : TopLevelCommand("delete", serviceProvider);

