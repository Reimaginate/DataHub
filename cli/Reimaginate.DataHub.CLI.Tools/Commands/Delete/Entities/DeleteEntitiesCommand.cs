using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Delete.Entities;

public class DeleteEntitiesCommand(IServiceProvider serviceProvider) : SubCommand<DeleteCommand>("entities", serviceProvider);

