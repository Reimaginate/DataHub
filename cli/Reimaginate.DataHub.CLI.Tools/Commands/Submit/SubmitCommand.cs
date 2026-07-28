using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Submit;

public class SubmitCommand(IServiceProvider serviceProvider) : TopLevelCommand("submit", serviceProvider);

