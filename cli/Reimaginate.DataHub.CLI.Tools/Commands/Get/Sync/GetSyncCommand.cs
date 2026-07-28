using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Sync;

public class GetSyncCommand(IServiceProvider serviceProvider) : SubCommand<GetCommand>("sync", "", serviceProvider);