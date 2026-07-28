using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Alerts;

public class GetAlertsCommand(IServiceProvider serviceProvider) : SubCommand<GetCommand>("alerts", "Inspect DataHub alerts", serviceProvider);
