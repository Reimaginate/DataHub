using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Merge;

public class GetMergeCommand(IServiceProvider serviceProvider) : SubCommand<GetCommand>("merge", "", serviceProvider);