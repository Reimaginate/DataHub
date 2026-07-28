using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Patch;

public class PatchCommand(IServiceProvider serviceProvider) : TopLevelCommand("patch", serviceProvider);