using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Patch;

public class GetPatchCommand(IServiceProvider serviceProvider) : SubCommand<GetCommand>("patch", "", serviceProvider);