using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Logs;

public class GetLogsCommand(IServiceProvider serviceProvider) : SubCommand<GetCommand>("logs", "Query DataHub log entries", serviceProvider);
