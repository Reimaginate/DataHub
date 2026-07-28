using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Entities;

public class GetEntitiesCommand(IServiceProvider serviceProvider) : SubCommand<GetCommand>("entities", serviceProvider);