using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Duplicates;

public class GetDuplicatesCommand(IServiceProvider serviceProvider) : SubCommand<GetCommand>("duplicates", "Inspect duplicate records", serviceProvider);
